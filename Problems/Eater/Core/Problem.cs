using Solve;
using System;
using System.Collections.Immutable;
using System.Diagnostics;

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

		const int FoodFoundRate = 0;
		const int AverageEnergy = 1;
		const int AveargeWasted = 2;

		protected static Fitness Fitness01(Genome genome, double[] metrics)
			=> new Fitness(Metrics01,
				metrics[FoodFoundRate],
				-metrics[AveargeWasted],
				-metrics[AverageEnergy],
				-genome.GeneCount);

		protected static Fitness Fitness02(Genome genome, double[] metrics)
			=> new Fitness(Metrics02,
				metrics[FoodFoundRate],
				-metrics[AveargeWasted],
				-genome.GeneCount,
				-metrics[AverageEnergy]);

		protected static readonly ImmutableArray<Metric> Metrics01 = new[]
		{
			new Metric(0, "Food-Found-Rate", "Food-Found-Rate {0:p}", 1),
			new Metric(1, "Average-Wasted", "Average-Wasted {0:n3}"),
			new Metric(2, "Average-Energy", "Average-Energy {0:n3}"),
			new Metric(3, "Gene-Count", "Gene-Count {0:n0}")
		}.ToImmutableArray();

		protected static readonly ImmutableArray<Metric> Metrics02 = new[]
		{
			Metrics01[0],
			Metrics01[1],
			Metrics01[3],
			Metrics01[2]
		}.ToImmutableArray();

		protected override double[] ProcessSampleMetrics(Genome g, long sampleId)
		{
			var boundary = Samples.Boundary;
			var samples = Samples.Get((int)sampleId);
			var count = SampleSize;
			double found = 0;
			double energy = 0;
			double wasted = 0;

			for (var i = 0; i < count; i++)
			{
				var s = samples[i];
				var success = g.Try(boundary, s.EaterStart, s.Food, out var e, out var w);
				if (success) found++;

				Debug.Assert(!g.TryReduce(out var red) || success == red.Try(boundary, s.EaterStart, s.Food),
					"Reduced version should match.");

				energy += e;
				wasted += w;
			}

			Debug.Assert(g.Hash.Length != 0 || found == 0,
				"An empty has should yield no results.");

			var averageEnergy = energy / count;
			var averageWasted = wasted / count;
			return new[] {
				found / count,
				averageEnergy,
				averageWasted
			};
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
