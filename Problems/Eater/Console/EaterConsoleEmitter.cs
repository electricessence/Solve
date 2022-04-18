﻿using Open.Threading;
using Solve;
using Solve.Experiment.Console;
using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace Eater;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
public class EaterConsoleEmitter : ConsoleEmitterBase<Genome>
{
	static readonly ImageCodecInfo JpgEncoder = ImageCodecInfo.GetImageEncoders().Single(e => e.MimeType == "image/jpeg");
	static readonly EncoderParameters EncParams = new(1)
	{
		Param = { [0] = new EncoderParameter(Encoder.Quality, 10L) }
	};

	readonly string ProgressionRootPath;
	readonly string ProgressionDirectoryPath;
	public ImmutableArray<string> PreviousWinners { get; private set; }

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

		emitter.PreviousWinners = !progression!.Exists
			? ImmutableArray<string>.Empty
			: progression
				.EnumerateDirectories()
				.OrderBy(d => d.Name).LastOrDefault()?
				.EnumerateFiles("*.txt")
				.GroupBy(file =>
				{
					var name = file.Name;
					var i = name.IndexOf('.');
					return i == -1 ? string.Empty : name[i..];
				})
				.Select(g => g.OrderBy(file => file.Name).Last())
				.Select(file =>
				{
					using var reader = file.OpenText();
					return reader.ReadToEnd().Trim();
				})
				.ToImmutableArray()
				?? ImmutableArray<string>.Empty;

		current.Create();
		return emitter;
	}

	public string SaveGenomeImage(Genome genome, string fileName)
	{
		var rendered = Path.Combine(ProgressionDirectoryPath, $"{fileName}.jpg");
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

		// Each winner needs a record and can only be guaranteed timely if this handler does it synchronously.
		// Step 1: render each 

		var suffix = $"{p.ID}.{poolIndex}";
		var fileName = $"{DateTime.Now.Ticks}.{suffix}";
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
		var locked = ThreadSafety.TryLock(queue, () =>
		{
			while (queue.TryDequeue(out var lastRendered))
			{
				// drain the queue.
				while (queue.TryDequeue(out var g))
					lastRendered = g;

				var latestFileName = Path.Combine(ProgressionRootPath, $"LatestWinner.{suffix}.jpg");
				try
				{
					File.Copy(lastRendered, Path.Combine(Environment.CurrentDirectory, latestFileName), true);
				}
				catch (IOException ex)
				{
					Debug.WriteLine($"Could not update {latestFileName}:\n" + ex.ToString());
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
}
