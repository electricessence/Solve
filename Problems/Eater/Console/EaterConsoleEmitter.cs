using Open.Threading;
using Solve;
using Solve.Experiment.Console;
using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace Eater
{
	public class EaterConsoleEmitter : ConsoleEmitterBase<Genome>
	{
		static readonly ImageCodecInfo JpgEncoder = ImageCodecInfo.GetImageEncoders().Single(e => e.MimeType == "image/jpeg");
		static readonly EncoderParameters EncParams = new EncoderParameters(1)
		{
			Param = { [0] = new EncoderParameter(Encoder.Quality, 20L) }
		};

		readonly string ProgressionDirectoryPath;
		public ImmutableArray<string> PreviousWinners { get; private set; }

		protected EaterConsoleEmitter(uint sampleMinimum)
			: base(sampleMinimum /*, Path.Combine(Environment.CurrentDirectory, $"Log-{DateTime.Now.Ticks}.csv")*/)
		{
			ProgressionDirectoryPath = Path.Combine(Environment.CurrentDirectory, "Progression", DateTime.Now.Ticks.ToString());
		}

		public static EaterConsoleEmitter Create(uint sampleMinimum = 50)
		{
			var emitter = new EaterConsoleEmitter(sampleMinimum);
			var current = new DirectoryInfo(emitter.ProgressionDirectoryPath);
			var progression = current.Parent;

			emitter.PreviousWinners = !progression.Exists
				? ImmutableArray<string>.Empty
				: progression
					.EnumerateDirectories()
					.OrderBy(d => d.Name).LastOrDefault()?
					.EnumerateFiles("*.txt")
					.GroupBy(file =>
					{
						var name = file.Name;
						var i = name.IndexOf('.');
						if (i == -1) return string.Empty;
						return name.Substring(i);
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
			return rendered;
		}

		//readonly ConcurrentDictionary<string, ProcedureResult[]> FullTests
		//	= new ConcurrentDictionary<string, ProcedureResult[]>();

		//public void EmitTopGenomeFullStats((IProblem<EaterGenome> Problem, EaterGenome Genome) kvp)
		//	=> EmitTopGenomeFullStats(kvp.Problem, kvp.Genome);

		//public void EmitTopGenomeFullStats(IProblem<EaterGenome> p, EaterGenome genome)
		//	=> EmitTopGenomeStatsInternal(p, genome, new Fitness(FullTests.GetOrAdd(genome.Hash, key => Samples.TestAll(key))));

		readonly ConcurrentDictionary<string, ConcurrentQueue<string>> BitmapQueue
			= new ConcurrentDictionary<string, ConcurrentQueue<string>>();

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

			var queue = BitmapQueue
				.GetOrAdd(suffix, key => new ConcurrentQueue<string>());

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

					var latestFileName = $"LatestWinner.{suffix}.jpg";
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
}
