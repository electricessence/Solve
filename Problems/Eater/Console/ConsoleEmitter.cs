using Open.Collections;
using Open.Numeric;
using Open.Threading;
using Solve;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;

namespace Eater
{
	public class ConsoleEmitter
	{
		public readonly SampleCache Samples;
		public readonly uint SampleMinimum;

		public ConsoleEmitter(SampleCache samples, uint sampleMinimum = 50)
		{
			Samples = samples;
			SampleMinimum = sampleMinimum;
		}

		readonly ConcurrentDictionary<string, ProcedureResult[]> FullTests = new ConcurrentDictionary<string, ProcedureResult[]>();

		public FitnessScore? LastScore;
		public string LastHash;
		public SynchronizedConsole.Message LastTopGenomeUpdate;

		public void EmitTopGenomeFullStats((IProblem<EaterGenome> Problem, EaterGenome Genome) kvp)
		{
			EmitTopGenomeFullStats(kvp.Problem, kvp.Genome);
		}

		public void EmitTopGenomeFullStats(IProblem<EaterGenome> p, EaterGenome genome)
		{
			EmitTopGenomeStatsInternal(p, genome, new Fitness(FullTests.GetOrAdd(genome.Hash, key => Samples.TestAll(key))));
		}

		public void EmitTopGenomeStats((IProblem<EaterGenome> Problem, EaterGenome Genome) kvp)
		{
			EmitTopGenomeStatsInternal(kvp.Problem, kvp.Genome);
		}

		public void EmitTopGenomeStats(IProblem<EaterGenome> p, EaterGenome genome)
		{
			EmitTopGenomeStatsInternal(p, genome);
		}

		protected bool EmitTopGenomeStatsInternal(IProblem<EaterGenome> p, EaterGenome genome, IFitness fitness = null)
		{
			var f = (fitness ?? p.GetFitnessFor(genome).Value.Fitness).SnapShot();

			var asReduced = genome.AsReduced();
			return ThreadSafety.LockConditional(
				SynchronizedConsole.Sync,
				() => f.SampleCount > SampleMinimum && (!LastScore.HasValue || LastScore.Value < f || LastHash == genome.Hash),
				() => SynchronizedConsole.OverwriteIfSame(ref LastTopGenomeUpdate, () => LastHash == genome.Hash,
					cursor =>
					{
						if (asReduced == genome)
							Console.WriteLine("{0}:\t{1}", p.ID, genome.Hash);
						else
							Console.WriteLine("{0}:\t{1}\n=>\t{2}", p.ID, genome.Hash, asReduced.Hash);

						EmitFitnessScoreWithLabels(f);
						Console.WriteLine();

						LastScore = f;
						LastHash = genome.Hash;

						using (var bitmap = genome.Genes.Render())
						{
							// Expand the size for clarity.
							var newDim = new Rectangle(0, 0, bitmap.Width * 4, bitmap.Height * 4);
							using (var newImage = new Bitmap(newDim.Width, newDim.Height))
							using (Graphics gr = Graphics.FromImage(newImage))
							{
								gr.SmoothingMode = SmoothingMode.None;
								gr.InterpolationMode = InterpolationMode.NearestNeighbor;
								gr.PixelOffsetMode = PixelOffsetMode.Default;
								gr.DrawImage(bitmap, newDim);
								newImage.Save(Path.Combine(Environment.CurrentDirectory, "LatestWinner.jpg"));
							}
						}

					}));
		}

		public void EmitFitnessScoreWithLabels(IFitness fitness)
		{
			var scoreStrings = new List<string>();
			var scores = fitness.Scores.ToArray();
			var len = scores.Length;
			for (var i = 0; i < len; i++)
			{
				scoreStrings.Add(String.Format(ProblemFragmented.FitnessLabels[i], scores[i]));
			}

			Console.WriteLine("  \t{0} ({1:n0} samples)", scoreStrings.JoinToString(", "), fitness.SampleCount);
		}

	}
}
