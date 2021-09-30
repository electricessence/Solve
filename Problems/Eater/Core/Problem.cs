using Open.Numeric;
using Solve;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

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
			: base(fitnessTranslators, sampleSize, championPoolSize) => Samples = new SampleCache(gridSize);

		const int FoodFoundRate = 0;
		const int AverageEnergy = 1;
		const int AverageWasted = 2;
		const int GeneCount = 3;

		static Fitness GetPrimaryMetricValues(ImmutableArray<Metric> metrics, IGenome genome, double[] values)
		{
			var len = metrics.Length;
			var result = ImmutableArray.CreateBuilder<double>(metrics.Length);
			result.Count = len;
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
			return new Fitness(in metrics, result.MoveToImmutable());
		}

		protected static readonly ImmutableArray<Metric> MetricsPrimary
			= ImmutableArray.Create(
				new Metric(FoodFoundRate, "Food-Found-Rate", "Food-Found-Rate {0:p}", 1),
				new Metric(AverageEnergy, "Average-Energy", "Average-Energy {0:n3}"),
				new Metric(AverageWasted, "Average-Wasted", "Average-Wasted {0:n3}"),
				new Metric(GeneCount, "Gene-Count", "Gene-Count {0:n0}"));

		protected static Fitness FitnessPrimary(Genome genome, double[] metrics)
			=> GetPrimaryMetricValues(MetricsPrimary, genome, metrics);

		protected static readonly ImmutableArray<Metric> MetricsSecondary01
			= ImmutableArray.Create(
				MetricsPrimary[FoodFoundRate],
				MetricsPrimary[AverageEnergy],
				MetricsPrimary[GeneCount]);

		protected static Fitness FitnessSecondary01(Genome genome, double[] metrics)
			=> GetSecondaryMetricValues(MetricsSecondary01, genome, metrics);

		protected static readonly ImmutableArray<Metric> MetricsSecondary02
			= ImmutableArray.Create(
				MetricsPrimary[FoodFoundRate],
				MetricsPrimary[GeneCount],
				MetricsPrimary[AverageEnergy]);

		protected static Fitness FitnessSecondary02(Genome genome, double[] metrics)
			=> GetSecondaryMetricValues(MetricsSecondary02, genome, metrics);



		static Fitness GetSecondaryMetricValues(ImmutableArray<Metric> metrics, Genome genome, double[] values)
		{
			var len = metrics.Length;
			var result = ImmutableArray.CreateBuilder<double>(metrics.Length);
			result.Count = len;
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
			return new Fitness(in metrics, result.MoveToImmutable());
		}

		protected static Fitness Fitness02(Genome genome, double[] metrics)
			=> GetPrimaryMetricValues(Metrics02, genome, metrics);

		protected static Fitness Fitness03(Genome genome, double[] metrics)
			=> GetPrimaryMetricValues(Metrics03, genome, metrics);

		protected static readonly ImmutableArray<Metric> Metrics02 = ImmutableArray.Create(MetricsPrimary[FoodFoundRate], MetricsPrimary[AverageWasted], MetricsPrimary[GeneCount], MetricsPrimary[AverageEnergy]);

		protected static readonly ImmutableArray<Metric> Metrics03 = ImmutableArray.Create(MetricsPrimary[FoodFoundRate], MetricsPrimary[GeneCount], MetricsPrimary[AverageEnergy], MetricsPrimary[AverageWasted]);

		protected override double[] ProcessSampleMetrics(Genome g, long sampleId)
		{
			var boundary = Samples.Boundary;
			var samples = Samples.Get((int)sampleId);
			samples = sampleId == -1 ? samples : samples.Take(SampleSize);
			double found = 0;
			double energy = 0;
			double wasted = 0;

			var count = 0;
			foreach (var s in samples)
			{
				count++;
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


		public double[] TestAllSamples(Genome g)
			=> ProcessSampleMetrics(g, -1);

		public ProcedureResult[] TestAll(Genome genome)
			=> TestAll(genome.Hash);

		public static Problem CreateFitnessPrimary(
			ushort gridSize = 10,
			ushort sampleSize = 40,
			ushort championPoolSize = 100)
			=> new(gridSize, sampleSize, championPoolSize, (MetricsPrimary, FitnessPrimary));

		public static Problem CreateFitnessSecondary(
			ushort gridSize = 10,
			ushort sampleSize = 40,
			ushort championPoolSize = 100)
			=> new(gridSize, sampleSize, championPoolSize, (MetricsSecondary01, FitnessSecondary01), (MetricsSecondary02, FitnessSecondary02));

		public static Problem CreateF02(
			ushort gridSize = 10,
			ushort sampleSize = 40,
			ushort championPoolSize = 100)
			=> new(gridSize, sampleSize, championPoolSize, (Metrics02, Fitness02));

		public static Problem CreateF0102(
			ushort gridSize = 10,
			ushort sampleSize = 40,
			ushort championPoolSize = 100)
			=> new(gridSize, sampleSize, championPoolSize, (MetricsPrimary, FitnessPrimary), (Metrics02, Fitness02));

		public static Problem CreateF010203(
			ushort gridSize = 10,
			ushort sampleSize = 40,
			ushort championPoolSize = 100)
			=> new(gridSize, sampleSize, championPoolSize, (MetricsPrimary, FitnessPrimary), (Metrics02, Fitness02), (Metrics03, Fitness03));

	}

}
