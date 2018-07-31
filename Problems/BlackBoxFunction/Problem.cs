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

	public class Problem : ProblemBase<EvalGenome>
	{
		protected static readonly IReadOnlyList<Metric> Metrics01 = new List<Metric>
		{
			new Metric(0, "Correlation", "Correlation {0:p10}", 1, double.Epsilon),
			new Metric(0, "Divergence", "Divergence {0:n1}", 0, 0.0000000000001),
			new Metric(2, "Gene-Count", "Gene-Count {0:n0}")
		}.AsReadOnly();

		protected static readonly IReadOnlyList<Metric> Metrics02 = new List<Metric>
		{
			Metrics01[1],
			Metrics01[0],
			Metrics01[2]
		}.AsReadOnly();

		protected static Fitness Fitness01(EvalGenome genome, double[] metrics)
			=> new Fitness(Metrics01, metrics[0], -metrics[1], -genome.GeneCount);

		protected static Fitness Fitness02(EvalGenome genome, double[] metrics)
			=> new Fitness(Metrics02, -metrics[1], metrics[0], -genome.GeneCount);

		public readonly SampleCache Samples;

		public Problem(Formula actualFormula,
			ushort sampleSize = 100,
			ushort championPoolSize = 100,
			params (IReadOnlyList<Metric> Metrics, Func<EvalGenome, double[], Fitness> Transform)[] fitnessTranslators)
			: base(fitnessTranslators, sampleSize, championPoolSize)
		{
			Samples = new SampleCache(actualFormula);
		}

		protected override double[] ProcessSampleMetrics(EvalGenome g, long sampleId)
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
					double.PositiveInfinity
				};
			}

			var c = correct.Correlation(calc);
			if (c > 1) c = 1; // Must clamp double precision insanity.
			else if (c.IsPreciseEqual(1)) c = 1; // Compensate for epsilon.

			//if (c > 1) c = 3 - 2 * c; // Correlation compensation for double precision insanity.
			var d = divergence.Where(v => !double.IsNaN(v)).Average();

			return new[] {
				(double.IsNaN(c) || double.IsInfinity(c)) ? -2 : c,
				(double.IsNaN(d) || double.IsInfinity(d)) ? double.PositiveInfinity : d
			};
		}

		public static Problem Create(
			Formula actualFormula,
			ushort sampleSize = 40,
			ushort championPoolSize = 100)
			=> new Problem(actualFormula, sampleSize, championPoolSize, (Metrics02, Fitness02), (Metrics01, Fitness01));

	}


}
