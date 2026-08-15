using Open.Arithmetic;
using Open.Numeric.Precision;
using Solve;
using Solve.Evaluation;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Multiplexer;

public delegate bool Formula(IReadOnlyList<bool> p);

public class Problem(Formula actualFormula,
	ushort sampleSize = 100,
	ushort championPoolSize = 100,
	params (ImmutableArray<Metric> Metrics, Func<EvalGenome<bool>, double[], Fitness> Transform)[] fitnessTranslators) : ProblemBase<EvalGenome<bool>>(fitnessTranslators, sampleSize, championPoolSize)
{
	protected static readonly ImmutableArray<Metric> Metrics01 = [
		new Metric(0, "Direction", "Direction {0:p1}", 1, double.Epsilon),
		new Metric(0, "Correlation", "Correlation {0:p10}", 1, double.Epsilon),
		new Metric(0, "Divergence", "Divergence {0:n1}", 0, 0.0000000000001),
		new Metric(2, "Gene-Count", "Gene-Count {0:n0}")
	];

	protected static readonly ImmutableArray<Metric> Metrics02 = [
		Metrics01[0],
		Metrics01[2],
		Metrics01[1],
		Metrics01[3]
	];

	protected static Fitness Fitness01(EvalGenome<bool> genome, double[] metrics)
		=> new(Metrics01, metrics[0], metrics[1], -metrics[2], -genome.GeneCount);

	protected static Fitness Fitness02(EvalGenome<bool> genome, double[] metrics)
		=> new(Metrics02, metrics[0], -metrics[2], metrics[1], -genome.GeneCount);

	public readonly SampleCache Samples = new(actualFormula);

	protected override double[] ProcessSampleMetrics(EvalGenome<bool> g, long sampleId)
	{
		var samples = Samples.Get(sampleId);
		var correct = new double[SampleSizeInt];
		var divergence = new double[SampleSizeInt];
		var calc = new double[SampleSizeInt];

		for (var i = 0; i < SampleSizeInt; i++) // Parallel here if futile since there are other threads running this for other genomes.
		{
			var sample = samples[i];
			Debug.Assert(sample != null);
			var s = sample.ParamValues;
			// Booleans are total (never NaN/undefined), so unlike the numeric
			// BlackBoxFunction sibling there's no NaN-detection branch needed here --
			// map true/false to 1/0 and reuse the same correlation-based fitness shape.
			var correctValue = sample.Correct.Value ? 1d : 0d;
			correct[i] = correctValue;
			var result = g.Evaluate(s) ? 1d : 0d;
			calc[i] = result;
			divergence[i] = Math.Abs(result - correctValue) * 10; // Averages can get too small.
		}

		// Attempt to detect non-linear relationships...
		var correct_dc = DeltasFixed(correct);
		var dc = DeltasFixed(calc);

		var dcCorrelation = correct_dc.Correlation(dc);

		var c = correct.Correlation(calc);
		if (c > 1) c = 1; // Must clamp double precision insanity.
		else if (c.IsPreciseEqual(1)) c = 1; // Compensate for epsilon.

		var d = divergence.Average();

		return [
			(double.IsNaN(dcCorrelation) || double.IsInfinity(dcCorrelation)) ? -2 : dcCorrelation,
			(double.IsNaN(c) || double.IsInfinity(c)) ? -2 : c,
			(double.IsNaN(d) || double.IsInfinity(d)) ? double.PositiveInfinity : d
		];
	}

	public static Problem Create(
		Formula actualFormula,
		ushort sampleSize = 40,
		ushort championPoolSize = 100)
		=> new(actualFormula, sampleSize, championPoolSize, (Metrics01, Fitness01), (Metrics02, Fitness02));

	static IEnumerable<double> DeltasFixed(IEnumerable<double> source)
		=> Deltas(source).Select(v =>
		{
			if (v > 0) return +1;
			if (v < 0) return -1;
			// equal
			return v;
		});

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
