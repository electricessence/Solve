using Open.Threading;
using Solve;
using Solve.Experiment.Console;
using System;
using System.Collections.Concurrent;
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


		public EaterConsoleEmitter(uint sampleMinimum = 50)
			: base(sampleMinimum /*, Path.Combine(Environment.CurrentDirectory, $"Log-{DateTime.Now.Ticks}.csv")*/)
		{
			ProgressionDirectory = Path.Combine(Environment.CurrentDirectory, "Progression", DateTime.Now.Ticks.ToString());
			Directory.CreateDirectory(ProgressionDirectory);
		}

		readonly string ProgressionDirectory;

		public string SaveGenomeImage(Genome genome, string fileName)
		{
			var rendered = Path.Combine(ProgressionDirectory, $"{fileName}.jpg");
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
		readonly object LatestWinnerImageLock = new object();

		protected override void OnEmittingGenomeFitness(IProblem<Genome> p, Genome genome, int poolIndex, Fitness fitness)
		{
			base.OnEmittingGenomeFitness(p, genome, poolIndex, fitness);

			var suffix = $"{p.ID}.{poolIndex}";
			var fileName = $"{DateTime.Now.Ticks}.{suffix}";
			try
			{
				File.WriteAllText(Path.Combine(ProgressionDirectory, $"{fileName}.txt"), genome.Hash);
			}
			catch (IOException)
			{

			}

			try
			{
				BitmapQueue
					.GetOrAdd(suffix, key => new ConcurrentQueue<string>())
					.Enqueue(SaveGenomeImage(genome, fileName));

				TryUpdateImages();
			}
			catch (InvalidOperationException)
			{

			}

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

		void TryUpdateImages()
		{
			ThreadSafety.TryLock(LatestWinnerImageLock, () =>
			{
				bool updated;
				do
				{
					updated = false;
					foreach (var key in BitmapQueue.Keys.ToArray())
					{
						var bq = BitmapQueue[key];
						string? lastRendered = null;
						while (bq.TryDequeue(out var g))
							lastRendered = g;
						if (lastRendered == null)
							continue;

						updated = true;
						var latestFileName = $"LatestWinner.{key}.jpg";
						try
						{
							File.Copy(lastRendered, Path.Combine(Environment.CurrentDirectory, latestFileName), true);
						}
						catch (IOException)
						{
							Debug.WriteLine($"Could not update {latestFileName}.");
						}
					}
				} while (updated);
			});
		}
	}
}
