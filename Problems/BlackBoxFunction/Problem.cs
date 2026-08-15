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

public class Problem(Formula actualFormula,
	ushort sampleSize = 100,
	ushort championPoolSize = 100,
	params (ImmutableArray<Metric> Metrics, Func<EvalGenome<double>, double[], Fitness> Transform)[] fitnessTranslators) : ProblemBase<EvalGenome<double>>(fitnessTranslators, sampleSize, championPoolSize)
{
	const int Direction = 0;
	const int Correlation = 1;
	const int Divergence = 2;
	const int GeneCount = 3;

	static Fitness GetPrimaryMetricValues(ImmutableArray<Metric> metrics, EvalGenome<double> genome, double[] values)
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
		= [
			// Tolerance 1e-7: Direction/Correlation are Pearson correlation values in [-1, 1]
			// computed from finite-sample double-precision arithmetic, so a "perfect" genome
			// almost never lands on exactly 1.0. double.Epsilon (the smallest subnormal double)
			// is effectively zero tolerance and can never be reached in practice. 1e-7 is loose
			// enough to accept the residual floating-point noise of a truly ideal fit while
			// still requiring six-nines-plus correlation, so it won't fire on a mediocre genome.
			new Metric(Direction, "Direction", "Direction {0:p1}", 1, 1e-7),
			new Metric(Correlation, "Correlation", "Correlation {0:p10}", 1, 1e-7),
			new Metric(Divergence, "Divergence", "Divergence {0:n1}", 0, 0.0000000000001),
			new Metric(GeneCount, "Gene-Count", "Gene-Count {0:n0}"),
		];

	protected static readonly ImmutableArray<Metric> Metrics02
		= [Metrics01[Correlation], Metrics01[Divergence], Metrics01[Direction], Metrics01[GeneCount]];

	protected static readonly ImmutableArray<Metric> Metrics03
		= [Metrics01[Correlation], Metrics01[Direction], Metrics01[Divergence], Metrics01[GeneCount]];

	protected static Fitness Fitness01(EvalGenome<double> genome, double[] metrics)
		=> GetPrimaryMetricValues(Metrics01, genome, metrics);

	protected static Fitness Fitness02(EvalGenome<double> genome, double[] metrics)
		=> GetPrimaryMetricValues(Metrics02, genome, metrics);

	protected static Fitness Fitness03(EvalGenome<double> genome, double[] metrics)
		=> GetPrimaryMetricValues(Metrics03, genome, metrics);

	public readonly SampleCache2 Samples = new(actualFormula, sampleSize);

	protected override double[] ProcessSampleMetrics(EvalGenome<double> g, long sampleId)
	{
		var samples = Samples.Get(sampleId);
		var pool = SampleSizeInt > 128 ? ArrayPool<double>.Shared : null;
		var correct = pool?.Rent(SampleSizeInt) ?? new double[SampleSizeInt];
		var divergence = pool?.Rent(SampleSizeInt) ?? new double[SampleSizeInt];
		var calc = pool?.Rent(SampleSizeInt) ?? new double[SampleSizeInt];
		try
		{
			var NaNcount = 0;

			// #if DEBUG
			// 			var gRed = g.AsReduced();
			// #endif

			for (var i = 0; i < SampleSizeInt; i++) // Parallel here is futile since there are other threads running this for other genomes.
			{
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
				return [
					NaNcount == SampleSizeInt // All NaN basically = fail.  Don't waste time trying to correlate.
						? double.NegativeInfinity
						: -2,
					NaNcount == SampleSizeInt // All NaN basically = fail.  Don't waste time trying to correlate.
						? double.NegativeInfinity
						: -2,
					double.PositiveInfinity
				];
			}

			// Attempt to detect non-linear relationships...
			var correct_dc = DeltasFixed(correct.Take(SampleSizeInt)).ToArray();
			var dc = DeltasFixed(calc.Take(SampleSizeInt));

			// A degenerate (constant-sign -- including all-zero -- or empty) delta-sign sequence in
			// the *target* means there's no direction to correlate against (e.g. a monotone target
			// whose deltas never change sign, or a level with fewer than 2 samples). Correlating
			// against a zero-variance series is mathematically undefined (NaN) regardless of how well
			// the genome fits, so that's not a genome failure: score it neutrally instead of letting
			// it fall into the NaN/-2 penalty below. That penalty remains reserved for correlations
			// that are undefined because of the genome's own (mis)behavior (e.g. a constant output
			// against a genuinely varying target).
			var dcCorrelation = IsDegenerateSignSequence(correct_dc)
				? 0
				: correct_dc.Correlation(dc);
			// Must clamp double precision insanity; an unclamped correlation above a metric's
			// MaxValue otherwise poisons convergence checks downstream.
			if (dcCorrelation > 1) dcCorrelation = 1;
			else if (dcCorrelation < -1) dcCorrelation = -1;
			else if (dcCorrelation.IsPreciseEqual(1)) dcCorrelation = 1;

			var c = correct.AsSpan(0, SampleSizeInt).Correlation(calc.AsSpan(0, SampleSizeInt));
			if (c > 1) c = 1; // Must clamp double precision insanity.
			else if (c < -1) c = -1;
			else if (c.IsPreciseEqual(1)) c = 1; // Compensate for epsilon.

			//if (c > 1) c = 3 - 2 * c; // Correlation compensation for double precision insanity.
			var d = divergence.Take(SampleSizeInt).Where(v => !double.IsNaN(v)).Average();

			return [
				(double.IsNaN(dcCorrelation) || double.IsInfinity(dcCorrelation)) ? -2 : dcCorrelation,
				(double.IsNaN(c) || double.IsInfinity(c)) ? -2 : c,
				(double.IsNaN(d) || double.IsInfinity(d)) ? double.PositiveInfinity : d
			];
		}
		finally
		{
			pool?.Return(calc);
			pool?.Return(correct);
			pool?.Return(divergence);
		}
	}

	public static Problem Create(
		Formula actualFormula,
		ushort sampleSize = 100,
		ushort championPoolSize = 100)
		=> new(actualFormula, sampleSize, championPoolSize, (Metrics01, Fitness01), (Metrics02, Fitness02), (Metrics03, Fitness03));

	static IEnumerable<double> DeltasFixed(IEnumerable<double> source)
		=> Deltas(source).Select(v => v > 0 ? +1 : v < 0 ? -1 : v);

	/// <summary>
	/// True when a delta-sign sequence (as produced by <see cref="DeltasFixed"/>) carries no
	/// directional information: it's empty (fewer than 2 samples) or every sign is identical
	/// (a constant/monotone-without-reversal target, including a perfectly flat one where every
	/// sign is 0).
	/// </summary>
	static bool IsDegenerateSignSequence(IReadOnlyList<double> deltaSigns)
	{
		if (deltaSigns.Count == 0) return true;
		var first = deltaSigns[0];
		for (var i = 1; i < deltaSigns.Count; i++)
		{
			if (deltaSigns[i] != first) return false;
		}

		return true;
	}

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
