using Open.Arithmetic;
using Open.Numeric.Precision;
using Solve;
using Solve.Evaluation;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace BlackBoxFunction;

public delegate double Formula(IReadOnlyList<double> p);

public class Problem : ProblemBase<EvalGenome<double>>
{
	const int Direction = 0;
	const int Correlation = 1;
	const int Divergence = 2;
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
				Direction => values[Direction],
				Correlation => values[Correlation],
				Divergence => -values[Divergence],
				GeneCount => -genome.GeneCount,
				_ => throw new IndexOutOfRangeException()
			};
		}

		return new Fitness(metrics, result.MoveToImmutable());
	}

	protected static readonly ImmutableArray<Metric> Metrics01
		= ImmutableArray.Create(
			new Metric(Direction, "Direction", "Direction {0:p1}", 1, double.Epsilon),
			new Metric(Correlation, "Correlation", "Correlation {0:p10}", 1, double.Epsilon),
			new Metric(Divergence, "Divergence", "Divergence {0:n1}", 0, 0.0000000000001),
			new Metric(GeneCount, "Gene-Count", "Gene-Count {0:n0}"));

	protected static readonly ImmutableArray<Metric> Metrics02
		= ImmutableArray.Create(
			Metrics01[Correlation],
			Metrics01[Divergence],
			Metrics01[Direction],
			Metrics01[GeneCount]);

	protected static readonly ImmutableArray<Metric> Metrics03
		= ImmutableArray.Create(
			Metrics01[Correlation],
			Metrics01[Direction],
			Metrics01[Divergence],
			Metrics01[GeneCount]);

	protected static Fitness Fitness01(EvalGenome<double> genome, double[] metrics)
		=> GetPrimaryMetricValues(Metrics01, genome, metrics);

	protected static Fitness Fitness02(EvalGenome<double> genome, double[] metrics)
		=> GetPrimaryMetricValues(Metrics02, genome, metrics);

	protected static Fitness Fitness03(EvalGenome<double> genome, double[] metrics)
		=> GetPrimaryMetricValues(Metrics03, genome, metrics);

	public readonly SampleCache2 Samples;

	public Problem(Formula actualFormula,
		ushort sampleSize = 100,
		ushort championPoolSize = 100,
		params (ImmutableArray<Metric> Metrics, Func<EvalGenome<double>, double[], Fitness> Transform)[] fitnessTranslators)
		: base(fitnessTranslators, sampleSize, championPoolSize)
		=> Samples = new SampleCache2(actualFormula, sampleSize);

	protected override double[] ProcessSampleMetrics(EvalGenome<double> g, long sampleId)
	{
		var samples = Samples.Get(sampleId);
		var pool = SampleSizeInt > 128 ? ArrayPool<double>.Shared : null;
		var correct = pool?.Rent(SampleSizeInt) ?? new double[SampleSizeInt];
		var divergence = pool?.Rent(SampleSizeInt) ?? new double[SampleSizeInt];
		var calc = pool?.Rent(SampleSizeInt) ?? new double[SampleSizeInt];
		var NaNcount = 0;

		// #if DEBUG
		// 			var gRed = g.AsReduced();
		// #endif

		for (var i = 0; i < SampleSizeInt; i++) // Parallel here is futile since there are other threads running this for other genomes.
		{
			var sample = samples[i];
			var (s, correctValue) = samples[i];
			correct[i] = correctValue;
			var result = g.Evaluate(s);
			// #if DEBUG
			// 				if (gRed != g)
			// 				{
			// 					var rr = useAsync ? await gRed.EvaluateAsync(s) : gRed.Evaluate(s);
			// 					if (!g.Genes.OfType<ParameterGene>().Any(gg => gg.ID > 1) // For debugging/testing IDs greater than 1 are invalid so ignore.
			// 						&& !result.IsRelativeNearEqual(rr, 7))
			// 					{
			// 						var message = String.Format(
			// 							"Reduction calculation doesn't match!!! {0} => {1}\n\tSample: {2}\n\tresult: {3} != {4}",
			// 							g, gRed, s.JoinToString(", "), result, rr);
			// 						if (!result.IsNaN())
			// 							Debug.WriteLine(message);
			// 						else
			// 							Debug.WriteLine(message);
			// 					}
			// 				}
			// #endif
			if (!double.IsNaN(correctValue) && double.IsNaN(result)) NaNcount++;
			calc[i] = result;
			divergence[i] = Math.Abs(result - correctValue) * 10; // Averages can get too small.
		}

		if (NaNcount != 0)
		{
			// We do not yet handle NaN values gracefully yet so avoid correlation.
			return new[] {
				NaNcount == SampleSizeInt // All NaN basically = fail.  Don't waste time trying to correlate.
					? double.NegativeInfinity
					: -2,
				NaNcount == SampleSizeInt // All NaN basically = fail.  Don't waste time trying to correlate.
					? double.NegativeInfinity
					: -2,
				double.PositiveInfinity
			};
		}

		// Attempt to detect non-linear relationships...
		var correct_dc = DeltasFixed(correct.Take(SampleSizeInt));
		var dc = DeltasFixed(calc.Take(SampleSizeInt));

		var dcCorrelation = correct_dc.Correlation(dc);

		var c = correct.AsSpan(0, SampleSizeInt).Correlation(calc.AsSpan(0, SampleSizeInt));
		if (c > 1) c = 1; // Must clamp double precision insanity.
		else if (c.IsPreciseEqual(1)) c = 1; // Compensate for epsilon.

		//if (c > 1) c = 3 - 2 * c; // Correlation compensation for double precision insanity.
		var d = divergence.Take(SampleSizeInt).Where(v => !double.IsNaN(v)).Average();

		pool?.Return(calc);
		pool?.Return(correct);
		pool?.Return(divergence);

		return new[] {
			(double.IsNaN(dcCorrelation) || double.IsInfinity(dcCorrelation)) ? -2 : dcCorrelation,
			(double.IsNaN(c) || double.IsInfinity(c)) ? -2 : c,
			(double.IsNaN(d) || double.IsInfinity(d)) ? double.PositiveInfinity : d
		};
	}

	public static Problem Create(
		Formula actualFormula,
		ushort sampleSize = 100,
		ushort championPoolSize = 100)
		=> new(actualFormula, sampleSize, championPoolSize, (Metrics01, Fitness01), (Metrics02, Fitness02), (Metrics03, Fitness03));

	static IEnumerable<double> DeltasFixed(IEnumerable<double> source)
		=> Deltas(source).Select(v => v > 0 ? +1 : v < 0 ? -1 : v);

	static IEnumerable<double> Deltas(IEnumerable<double> source)
	{
		using var e = source.GetEnumerator();
		if (!e.MoveNext())
			yield break;

		var previous = e.Current;

		while (e.MoveNext())
		{
			var current = e.Current;
			yield return current - previous;
			previous = current;
		}
	}
}
