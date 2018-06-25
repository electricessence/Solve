﻿using Open.Threading;
using Solve.Experiment.Console;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace Eater
{
	public class EaterConsoleEmitter : ConsoleEmitterBase<EaterGenome>
	{
		static readonly ImageCodecInfo JpgEncoder;
		static readonly EncoderParameters EncParams;

		static EaterConsoleEmitter()
		{
			JpgEncoder = ImageCodecInfo.GetImageEncoders().Single(e => e.MimeType == "image/jpeg");
			EncParams = new EncoderParameters(1);
			EncParams.Param[0] = new EncoderParameter(Encoder.Quality, 20L);
		}

		public EaterConsoleEmitter(uint sampleMinimum = 50)
			: base(sampleMinimum, null/* Path.Combine(Environment.CurrentDirectory, $"Log-{DateTime.Now.Ticks}.csv")*/)
		{
			ProgressionDirectory = Path.Combine(Environment.CurrentDirectory, "Progression", DateTime.Now.Ticks.ToString());
			Directory.CreateDirectory(ProgressionDirectory);
		}

		readonly string ProgressionDirectory;

		//readonly ConcurrentDictionary<string, ProcedureResult[]> FullTests
		//	= new ConcurrentDictionary<string, ProcedureResult[]>();

		//public void EmitTopGenomeFullStats((IProblem<EaterGenome> Problem, EaterGenome Genome) kvp)
		//	=> EmitTopGenomeFullStats(kvp.Problem, kvp.Genome);

		//public void EmitTopGenomeFullStats(IProblem<EaterGenome> p, EaterGenome genome)
		//	=> EmitTopGenomeStatsInternal(p, genome, new Fitness(FullTests.GetOrAdd(genome.Hash, key => Samples.TestAll(key))));


		readonly ConcurrentQueue<string> BitmapQueue = new ConcurrentQueue<string>();
		readonly object LatestWinnerImageLock = new object();
		protected override void OnEmittingGenome(EaterGenome genome, string fitnessName, ReadOnlySpan<double> fitness, int sampleCount)
		{
			base.OnEmittingGenome(genome, fitnessName, fitness, sampleCount);
			var ts = DateTime.Now.Ticks;
			File.WriteAllText(Path.Combine(ProgressionDirectory, $"{ts}.txt"), genome.Hash);
			var rendered = Path.Combine(ProgressionDirectory, $"{ts}.jpg");
			var bitmap = genome.Genes.ToArray().Render2();
			if (bitmap != null)
			{
				using (bitmap)
					bitmap.Save(rendered, JpgEncoder, EncParams);
			}

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
					try
					{
						File.Copy(lastRendered, Path.Combine(Environment.CurrentDirectory, "LatestWinner.jpg"), true);
					}
					catch (IOException)
					{
						Debug.WriteLine("Could not update LatestWinner.jpg.");
					}
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
