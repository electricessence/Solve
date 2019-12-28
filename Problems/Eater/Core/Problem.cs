using Solve;
using System;
using System.Collections.Immutable;

namespace Eater
{
	public class Problem : ProblemBase<Genome>
	{
		public readonly SampleCache Samples;

		protected Problem(
			ushort gridSize = 10,
			ushort sampleSize = 40,
			ushort championPoolSize = 100,
			params (ImmutableArray<Metric> Metrics, Func<Genome, double[], Fitness> Transform)[] fitnessTranslators)
			: base(fitnessTranslators, sampleSize, championPoolSize)
		{
			Samples = new SampleCache(gridSize);
		}

		protected static Fitness Fitness01(Genome genome, double[] metrics)
			=> new Fitness(Metrics01, metrics[0], -metrics[1], -genome.GeneCount);

		protected static Fitness Fitness02(Genome genome, double[] metrics)
			=> new Fitness(Metrics02, metrics[0], -genome.GeneCount, -metrics[1]);

		protected static readonly ImmutableArray<Metric> Metrics01 = new[]
		{
			new Metric(0, "Food-Found-Rate", "Food-Found-Rate {0:p}", 1),
			new Metric(1, "Average-Energy", "Average-Energy {0:n3}"),
			new Metric(2, "Gene-Count", "Gene-Count {0:n0}")
		}.ToImmutableArray();

		protected static readonly ImmutableArray<Metric> Metrics02 = new[]
		{
			Metrics01[0],
			Metrics01[2],
			Metrics01[1]
		}.ToImmutableArray();

		protected override double[] ProcessSampleMetrics(Genome g, long sampleId)
		{
			var boundary = Samples.Boundary;
			var samples = Samples.Get((int)sampleId);
			var len = SampleSize;
			double found = 0;
			double energy = 0;

			for (var i = 0; i < len; i++)
			{
				var s = samples[i];
				if (g.Try(boundary, s.EaterStart, s.Food, out var e))
				{
					found++;
					//Debug.Assert(g.AsReduced().Try(boundary, s.EaterStart, s.Food), "Reduced version should match.");
				}
				else
				{
					//Debug.Assert(!g.AsReduced().Try(boundary, s.EaterStart, s.Food), "Reduced version should match.");
				}

				energy += e;
			}

			//Debug.Assert(g.Hash.Length != 0 || found == 0, "An empty has should yield no results.");

			var ave = energy / len;
			return new[] {
				found / len,
				ave
			};// - Math.Pow(ave, 2) - geneCount, ave, -geneCount); // Adding the geneCount seems superfluous but ends up being considered in the Pareto front.
		}

		public static Problem CreateF01(
			ushort gridSize = 10,
			ushort sampleSize = 40,
			ushort championPoolSize = 100)
			=> new Problem(gridSize, sampleSize, championPoolSize, (Metrics01, Fitness01));

		public static Problem CreateF02(
			ushort gridSize = 10,
			ushort sampleSize = 40,
			ushort championPoolSize = 100)
			=> new Problem(gridSize, sampleSize, championPoolSize, (Metrics02, Fitness02));

		public static Problem CreateF0102(
			ushort gridSize = 10,
			ushort sampleSize = 40,
			ushort championPoolSize = 100)
			=> new Problem(gridSize, sampleSize, championPoolSize, (Metrics01, Fitness01), (Metrics02, Fitness02));

	}

}
