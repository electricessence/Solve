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
		const int AverageWasted = 2;
		const int GeneCount = 3;


		protected static readonly ImmutableArray<Metric> MetricsPrimary = new[]
		{
			new Metric(FoodFoundRate, "Food-Found-Rate", "Food-Found-Rate {0:p}", 1),
			new Metric(AverageEnergy, "Average-Energy", "Average-Energy {0:n3}"),
			new Metric(AverageWasted, "Average-Wasted", "Average-Wasted {0:n3}"),
			new Metric(GeneCount, "Gene-Count", "Gene-Count {0:n0}")
		}.ToImmutableArray();

		protected static Fitness FitnessPrimary(Genome genome, double[] metrics)
			=> GetPrimaryMetricValues(MetricsPrimary, genome, metrics);

		protected static readonly ImmutableArray<Metric> MetricsSecondary01 = new[]
		{
			MetricsPrimary[FoodFoundRate],
			MetricsPrimary[AverageEnergy],
			MetricsPrimary[GeneCount]
		}.ToImmutableArray();

		protected static Fitness FitnessSecondary01(Genome genome, double[] metrics)
			=> GetSecondaryMetricValues(MetricsSecondary01, genome, metrics);

		protected static readonly ImmutableArray<Metric> MetricsSecondary02 = new[]
		{
			MetricsPrimary[FoodFoundRate],
			MetricsPrimary[GeneCount],
			MetricsPrimary[AverageEnergy]
		}.ToImmutableArray();

		protected static Fitness FitnessSecondary02(Genome genome, double[] metrics)
			=> GetSecondaryMetricValues(MetricsSecondary02, genome, metrics);

		static Fitness GetPrimaryMetricValues(ImmutableArray<Metric> metrics, Genome genome, double[] values)
		{
			var len = metrics.Length;
			var result = new double[metrics.Length];
			for (var i = 0; i < len; i++)
			{
				var metric = metrics[i];
				result[i] = metric.ID switch
				{
					FoodFoundRate => values[FoodFoundRate],
					AverageEnergy => -values[AverageEnergy],
					AverageWasted => -values[AverageWasted],
					GeneCount => -genome.GeneCount,
					_ => throw new IndexOutOfRangeException()
				};
			}
			return new Fitness(metrics, result);
		}

		static Fitness GetSecondaryMetricValues(ImmutableArray<Metric> metrics, Genome genome, double[] values)
		{
			var len = metrics.Length;
			var result = new double[metrics.Length];
			for (var i = 0; i < len; i++)
			{
				var metric = metrics[i];
				result[i] = metric.ID switch
				{
					FoodFoundRate => values[FoodFoundRate],
					AverageEnergy => -values[AverageEnergy] - values[AverageWasted] * 2,
					GeneCount => -genome.GeneCount,
					_ => throw new IndexOutOfRangeException()
				};
			}
			return new Fitness(metrics, result);
		}

		protected static Fitness Fitness02(Genome genome, double[] metrics)
			=> GetPrimaryMetricValues(Metrics02, genome, metrics);

		protected static Fitness Fitness03(Genome genome, double[] metrics)
			=> GetPrimaryMetricValues(Metrics03, genome, metrics);

		protected static readonly ImmutableArray<Metric> Metrics02 = new[]
		{
			MetricsPrimary[FoodFoundRate],
			MetricsPrimary[AverageWasted],
			MetricsPrimary[GeneCount],
			MetricsPrimary[AverageEnergy]
		}.ToImmutableArray();

		protected static readonly ImmutableArray<Metric> Metrics03 = new[]
		{
			MetricsPrimary[FoodFoundRate],
			MetricsPrimary[GeneCount],
			MetricsPrimary[AverageEnergy],
			MetricsPrimary[AverageWasted],
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

		public static Problem CreateFitnessPrimary(
			ushort gridSize = 10,
			ushort sampleSize = 40,
			ushort championPoolSize = 100)
			=> new Problem(gridSize, sampleSize, championPoolSize, (MetricsPrimary, FitnessPrimary));

		public static Problem CreateFitnessSecondary(
			ushort gridSize = 10,
			ushort sampleSize = 40,
			ushort championPoolSize = 100)
			=> new Problem(gridSize, sampleSize, championPoolSize, (MetricsSecondary01, FitnessSecondary01), (MetricsSecondary02, FitnessSecondary02));

		public static Problem CreateF02(
			ushort gridSize = 10,
			ushort sampleSize = 40,
			ushort championPoolSize = 100)
			=> new Problem(gridSize, sampleSize, championPoolSize, (Metrics02, Fitness02));

		public static Problem CreateF0102(
			ushort gridSize = 10,
			ushort sampleSize = 40,
			ushort championPoolSize = 100)
			=> new Problem(gridSize, sampleSize, championPoolSize, (MetricsPrimary, FitnessPrimary), (Metrics02, Fitness02));

		public static Problem CreateF010203(
			ushort gridSize = 10,
			ushort sampleSize = 40,
			ushort championPoolSize = 100)
			=> new Problem(gridSize, sampleSize, championPoolSize, (MetricsPrimary, FitnessPrimary), (Metrics02, Fitness02), (Metrics03, Fitness03));

	}

}
