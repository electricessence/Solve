using Open.Threading;
using Solve;
using Solve.Experiment.Console;
using Spectre.Console.Rendering;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace Eater;

#pragma warning disable IDE0079 // Remove unnecessary suppression
[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
#pragma warning restore IDE0079 // Remove unnecessary suppression
public class EaterConsoleEmitter : ConsoleEmitterBase<Genome>
{
	// Fix for https://github.com/dotnet/winforms/issues/12494
	static ImageCodecInfo GetJpgEncoding()
	{
		_ = Pens.Black;
		return ImageCodecInfo.GetImageEncoders().Single(e => e.MimeType == "image/jpeg");
	}

	static readonly ImageCodecInfo JpgEncoder = GetJpgEncoding();
	static readonly EncoderParameters EncParams = new(1)
	{
		Param = { [0] = new EncoderParameter(Encoder.Quality, 75L) }
	};

	readonly string ProgressionRootPath;
	readonly string ProgressionDirectoryPath;
	public ImmutableArray<string> PreviousWinners { get; private set; }

	// Task 15-0038: state backing BuildExtraPanels() below -- the live path-trace canvas,
	// per-pool champion cards, and gene-count trend indicator. Kept separate from the JPEG
	// pipeline above (which this class already had); populated from the same
	// OnEmittingGenomeFitness call that feeds ConsoleEmitterBase's own TopGenomeStats, so both
	// stay in sync with each other and with EaterLiveView.RepresentativeStart's implicit
	// "single start" scope boundary.
	readonly System.Threading.Lock _eaterViewSync = new();
	readonly ConcurrentDictionary<string, EaterChampionSnapshot> _championSnapshots = new();
	const int MaxGeneCountTrendEntries = 20; // Task 15-0038 AC4 asks for a minimum of the last 10.
	readonly List<int> _geneCountTrend = [];
	Size? _boundary;
	ImmutableArray<Point> _foodExamples = ImmutableArray<Point>.Empty;
	EaterChampionSnapshot? _bestOverall;

	/// <summary>
	/// Everything <see cref="EaterLiveView"/>'s panels need for one problem/pool's current
	/// champion, plus the genes needed to re-trace its path -- <see cref="TopGenomeStat"/> (the
	/// base class's own snapshot) only keeps a formatted composite fitness string, not the raw
	/// Food-Found-Rate/energy values task 15-0038 AC3's cards need individually.
	/// </summary>
	readonly record struct EaterChampionSnapshot(
		int ProblemId,
		int PoolIndex,
		ImmutableArray<Step> Genes,
		string Hash,
		int GeneCount,
		int SampleCount,
		double FoodFoundRate,
		double AverageEnergy);

	protected EaterConsoleEmitter(uint sampleMinimum)
		: base(sampleMinimum /*, Path.Combine(Environment.CurrentDirectory, $"Log-{DateTime.Now.Ticks}.csv")*/)
	{
		ProgressionRootPath = Path.Combine(Environment.CurrentDirectory, "Progression");
		ProgressionDirectoryPath = Path.Combine(ProgressionRootPath, DateTime.Now.Ticks.ToString());
	}

	public static EaterConsoleEmitter Create(uint sampleMinimum = 50)
	{
		var emitter = new EaterConsoleEmitter(sampleMinimum);
		var current = new DirectoryInfo(emitter.ProgressionDirectoryPath);
		var progression = current.Parent;

		emitter.PreviousWinners = !progression!.Exists ? []
			: progression
				.EnumerateDirectories()
				.OrderBy(d => d.Name).LastOrDefault()?
				.EnumerateFiles("*.txt")
				.GroupBy(file =>
				{
					string name = file.Name;
					int i = name.IndexOf('.');
					return i == -1 ? string.Empty : name[i..];
				})
				.Select(g => g.OrderBy(file => file.Name).Last())
				.Select(file =>
				{
					using var reader = file.OpenText();
					return reader.ReadToEnd().Trim();
				})
				.ToImmutableArray()
				?? [];

		current.Create();
		return emitter;
	}

	public string SaveGenomeImage(Genome genome, string fileName)
	{
		string rendered = Path.Combine(ProgressionDirectoryPath, $"{fileName}.jpg");
		using (var bitmap = genome.Genes.ToArray().Render2())
			bitmap.Save(rendered, JpgEncoder, EncParams);

		//var file = new FileInfo(rendered);
		//double before = file.Length;

		//var optimizer = new ImageOptimizer()
		//{
		//	OptimalCompression = true
		//};
		//optimizer.Compress(file);

		//file.Refresh();
		//Console.WriteLine("Size changed: {0:p2}", before / file.Length);

		return rendered;
	}

	//readonly ConcurrentDictionary<string, ProcedureResult[]> FullTests
	//	= new ConcurrentDictionary<string, ProcedureResult[]>();

	//public void EmitTopGenomeFullStats((IProblem<EaterGenome> Problem, EaterGenome Genome) kvp)
	//	=> EmitTopGenomeFullStats(kvp.Problem, kvp.Genome);

	//public void EmitTopGenomeFullStats(IProblem<EaterGenome> p, EaterGenome genome)
	//	=> EmitTopGenomeStatsInternal(p, genome, new Fitness(FullTests.GetOrAdd(genome.Hash, key => Samples.TestAll(key))));

	readonly ConcurrentDictionary<string, ConcurrentQueue<string>> BitmapQueue
		= new();

	// Avoid extra allocations
	static readonly Func<string, ConcurrentQueue<string>> ConcurrentQueueFactory = _ => new();

	protected override void OnEmittingGenomeFitness(IProblem<Genome> p, Genome genome, int poolIndex, Fitness fitness)
	{
		base.OnEmittingGenomeFitness(p, genome, poolIndex, fitness);

		RecordEaterChampion(p, genome, poolIndex, fitness);

		// Each winner needs a record and can only be guaranteed timely if this handler does it synchronously.
		// Step 1: render each

		string suffix = $"{p.ID}.{poolIndex}";
		string fileName = $"{DateTime.Now.Ticks}.{suffix}";
		try
		{
			File.WriteAllText(Path.Combine(ProgressionDirectoryPath, $"{fileName}.txt"), genome.Hash);
		}
		catch (IOException)
		{
		}

		var queue = BitmapQueue.GetOrAdd(suffix, ConcurrentQueueFactory);

		try
		{
			queue.Enqueue(SaveGenomeImage(genome, fileName));
		}
		catch (InvalidOperationException)
		{
		}

	retry:
		bool locked = ThreadSafety.TryLock(queue, () =>
		{
			while (queue.TryDequeue(out string? lastRendered))
			{
				// drain the queue.
				while (queue.TryDequeue(out string? g))
					lastRendered = g;

				string latestFileName = Path.Combine(ProgressionRootPath, $"LatestWinner.{suffix}.jpg");
				try
				{
					File.Copy(lastRendered, Path.Combine(Environment.CurrentDirectory, latestFileName), true);
				}
				catch (IOException ex)
				{
					Debug.WriteLine("Could not update {0}:\n", ex);
				}
			}
		});

		if (locked && !queue.IsEmpty)
			goto retry;

		//// Expand the size for clarity.
		//var newDim = new Rectangle(0, 0, bitmap.Width * 4, bitmap.Height * 4);
		//using (var newImage = new Bitmap(newDim.Width, newDim.Height))
		//using (var gr = Graphics.FromImage(newImage))
		//{
		//	gr.SmoothingMode = SmoothingMode.None;
		//	gr.InterpolationMode = InterpolationMode.NearestNeighbor;
		//	gr.PixelOffsetMode = PixelOffsetMode.Default;
		//	gr.DrawImage(bitmap, newDim);
		//	newImage.Save(Path.Combine(Environment.CurrentDirectory, "LatestWinner.jpg"));
		//}
	}

	/// <summary>
	/// Task 15-0038: updates the per-pool champion snapshot, the cross-pool "best genome for the
	/// path canvas" selection (AC2), and the gene-count trend (AC4) from the same champion update
	/// <see cref="OnEmittingGenomeFitness"/> already receives -- no separate subscription needed.
	/// </summary>
	void RecordEaterChampion(IProblem<Genome> p, Genome genome, int poolIndex, Fitness fitness)
	{
		double foodFoundRate = MetricAverage(fitness, "Food-Found-Rate");
		double averageEnergy = MetricAverage(fitness, "Average-Energy");

		var snapshot = new EaterChampionSnapshot(
			p.ID, poolIndex, genome.Genes, genome.Hash, genome.GeneCount, fitness.SampleCount, foodFoundRate, averageEnergy);

		_championSnapshots[$"{p.ID}.{poolIndex}"] = snapshot;

		lock (_eaterViewSync)
		{
			if (_boundary is null && p is Problem eaterProblem)
			{
				_boundary = eaterProblem.Samples.Boundary;
				// A small, stable set of real sample food positions (task 15-0038's Objective:
				// "start plus food-example cells highlighted") -- SampleCache.Get(0) is memoized,
				// so re-taking the first 3 entries always yields the same points.
				_foodExamples = [.. eaterProblem.Samples.Get(0).Take(3).Select(e => e.Food)];
			}

			var candidateRank = new EaterLiveView.ChampionRank(foodFoundRate, genome.GeneCount);
			EaterLiveView.ChampionRank? currentRank = _bestOverall is { } best
				? new EaterLiveView.ChampionRank(best.FoodFoundRate, best.GeneCount)
				: null;

			if (EaterLiveView.IsBetterChampion(candidateRank, currentRank))
			{
				_bestOverall = snapshot;

				if (candidateRank.IsPerfect)
				{
					_geneCountTrend.Add(genome.GeneCount);
					while (_geneCountTrend.Count > MaxGeneCountTrendEntries)
						_geneCountTrend.RemoveAt(0);
				}
			}
		}
	}

	static double MetricAverage(Fitness fitness, string metricNamePrefix)
	{
		foreach ((Metric metric, double value) in fitness.MetricAverages)
		{
			if (metric.Name.StartsWith(metricNamePrefix, StringComparison.Ordinal))
				return value;
		}

		return double.NaN;
	}

	/// <summary>
	/// Task 15-0038's contribution to the generic extra-panels hook
	/// (<see cref="ConsoleEmitterBase{TGenome}.BuildExtraPanels"/>): the champion-path canvas, the
	/// per-pool champion cards, and the gene-count trend indicator. Empty until the first Eater
	/// champion has been recorded (no boundary known yet) -- matches every other panel's "nothing
	/// yet" empty state rather than throwing.
	/// </summary>
	public override IReadOnlyList<IRenderable> BuildExtraPanels()
	{
		Size boundary;
		EaterChampionSnapshot? best;
		ImmutableArray<Point> foodExamples;
		int[] trend;

		lock (_eaterViewSync)
		{
			if (_boundary is not { } b) return [];
			boundary = b;
			best = _bestOverall;
			foodExamples = _foodExamples;
			trend = [.. _geneCountTrend];
		}

		ImmutableArray<Point> trace = best is { } champion
			? PathTracer.Trace(champion.Genes, boundary, EaterLiveView.RepresentativeStart)
			: [EaterLiveView.RepresentativeStart];

		EaterLiveView.ChampionCard[] cards = [.. _championSnapshots.Values
			.Select(s => new EaterLiveView.ChampionCard(s.ProblemId, s.PoolIndex, s.Hash, s.GeneCount, s.SampleCount, s.FoodFoundRate, s.AverageEnergy))];

		return EaterLiveView.BuildPanels(boundary, trace, foodExamples, EaterLiveView.RepresentativeStart, cards, trend);
	}
}
