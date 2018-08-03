using Open.Arithmetic;
using Open.Numeric.Precision;
using Solve;
using Solve.Evaluation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BlackBoxFunction
{
	public delegate double Formula(IReadOnlyList<double> p);

	public class Problem : ProblemBase<EvalGenome<double>>
	{
		protected static readonly IReadOnlyList<Metric> Metrics01 = new List<Metric>
		{
			new Metric(0, "Direction", "Direction {0:p1}", 1, double.Epsilon),
			new Metric(0, "Correlation", "Correlation {0:p10}", 1, double.Epsilon),
			new Metric(0, "Divergence", "Divergence {0:n1}", 0, 0.0000000000001),
			new Metric(2, "Gene-Count", "Gene-Count {0:n0}")
		}.AsReadOnly();

		protected static readonly IReadOnlyList<Metric> Metrics02 = new List<Metric>
		{
			Metrics01[0],
			Metrics01[2],
			Metrics01[1],
			Metrics01[3]
		}.AsReadOnly();

		protected static Fitness Fitness01(EvalGenome<double> genome, double[] metrics)
			=> new Fitness(Metrics01, metrics[0], metrics[1], -metrics[2], -genome.GeneCount);

		protected static Fitness Fitness02(EvalGenome<double> genome, double[] metrics)
			=> new Fitness(Metrics02, metrics[0], -metrics[2], metrics[1], -genome.GeneCount);

		public readonly SampleCache Samples;

		public Problem(Formula actualFormula,
			ushort sampleSize = 100,
			ushort championPoolSize = 100,
			params (IReadOnlyList<Metric> Metrics, Func<EvalGenome<double>, double[], Fitness> Transform)[] fitnessTranslators)
			: base(fitnessTranslators, sampleSize, championPoolSize)
		{
			Samples = new SampleCache(actualFormula);
		}

		protected override double[] ProcessSampleMetrics(EvalGenome<double> g, long sampleId)
		{
			var samples = Samples.Get(sampleId);
			var correct = new double[SampleSizeInt];
			var divergence = new double[SampleSizeInt];
			var calc = new double[SampleSizeInt];
			var NaNcount = 0;

			// #if DEBUG
			// 			var gRed = g.AsReduced();
			// #endif

			for (var i = 0; i < SampleSizeInt; i++) // Parallel here if futile since there are other threads running this for other genomes.
			{
				var sample = samples[i];
				Debug.Assert(sample != null);
				var s = sample.ParamValues;
				var correctValue = sample.Correct.Value;
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
			var correct_dc = DeltasFixed(correct);
			var dc = DeltasFixed(calc);

			var dcCorrelation = correct_dc.Correlation(dc);

			var c = correct.Correlation(calc);
			if (c > 1) c = 1; // Must clamp double precision insanity.
			else if (c.IsPreciseEqual(1)) c = 1; // Compensate for epsilon.

			//if (c > 1) c = 3 - 2 * c; // Correlation compensation for double precision insanity.
			var d = divergence.Where(v => !double.IsNaN(v)).Average();

			return new[] {
				(double.IsNaN(dcCorrelation) || double.IsInfinity(dcCorrelation)) ? -2 : dcCorrelation,
				(double.IsNaN(c) || double.IsInfinity(c)) ? -2 : c,
				(double.IsNaN(d) || double.IsInfinity(d)) ? double.PositiveInfinity : d
			};
		}

		public static Problem Create(
			Formula actualFormula,
			ushort sampleSize = 40,
			ushort championPoolSize = 100)
			=> new Problem(actualFormula, sampleSize, championPoolSize, (Metrics01, Fitness01), (Metrics02, Fitness02));

		static IEnumerable<double> DeltasFixed(IEnumerable<double> source)
			=> Deltas(source).Select(v =>
			{
				if (v > 0) return +1;
				if (v < 0) return -1;
				return v;
			});


		static IEnumerable<double> Deltas(IEnumerable<double> source)
		{
			using (var e = source.GetEnumerator())
			{
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
	}


}
