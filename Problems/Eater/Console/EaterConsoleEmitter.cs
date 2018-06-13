using Open.Numeric;
using Open.Threading;
using Solve;
using Solve.Experiment.Console;
using System;
using System.Collections.Concurrent;
using System.IO;

namespace Eater
{
	public class EaterConsoleEmitter : ConsoleEmitterBase<EaterGenome>
	{
		public readonly SampleCache Samples;

		public EaterConsoleEmitter(SampleCache samples, uint sampleMinimum = 50)
			: base(sampleMinimum, null/* Path.Combine(Environment.CurrentDirectory, $"Log-{DateTime.Now.Ticks}.csv")*/)
		{
			Samples = samples;
			ProgressionDirectory = Path.Combine(Environment.CurrentDirectory, "Progression", DateTime.Now.Ticks.ToString());
			Directory.CreateDirectory(ProgressionDirectory);
		}

		readonly string ProgressionDirectory;

		readonly ConcurrentDictionary<string, ProcedureResult[]> FullTests
			= new ConcurrentDictionary<string, ProcedureResult[]>();

		public void EmitTopGenomeFullStats((IProblem<EaterGenome> Problem, EaterGenome Genome) kvp)
			=> EmitTopGenomeFullStats(kvp.Problem, kvp.Genome);

		public void EmitTopGenomeFullStats(IProblem<EaterGenome> p, EaterGenome genome)
			=> EmitTopGenomeStatsInternal(p, genome, new Fitness(FullTests.GetOrAdd(genome.Hash, key => Samples.TestAll(key))));

		readonly ConcurrentQueue<string> BitmapQueue = new ConcurrentQueue<string>();
		readonly object LatestWinnerImageLock = new object();
		protected override void OnEmittingGenome(IProblem<EaterGenome> p, EaterGenome genome, IFitness fitness)
		{
			base.OnEmittingGenome(p, genome, fitness);
			var rendered = Path.Combine(ProgressionDirectory, $"{DateTime.Now.Ticks}.jpg");
			using (var bitmap = genome.Genes.Render2())
				bitmap.Save(rendered);

			BitmapQueue.Enqueue(rendered);
			ThreadSafety.TryLock(LatestWinnerImageLock, () =>
			{
				string lastRendered = null;
				while (BitmapQueue.TryDequeue(out string g))
				{
					lastRendered = g;
				}
				if (lastRendered != null)
				{
					File.Copy(lastRendered, Path.Combine(Environment.CurrentDirectory, "LatestWinner.jpg"), true);
				}
			});

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
