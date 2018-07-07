using Open.Arithmetic;
using Open.Numeric.Precision;
using Solve;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlackBoxFunction
{

	public delegate double Formula(IReadOnlyList<double> p);


	public class Problem : ProblemBase<Genome>
	{
		public readonly SampleCache Samples;

		public Problem(Formula actualFormula,
			ushort sampleSize = 40,
			ushort championPoolSize = 100,
			params (IReadOnlyList<Metric> Metrics, Func<Genome, double[], Fitness> Transform)[] fitnessTranslators)
			: base(fitnessTranslators, sampleSize, championPoolSize)
		{
			Samples = new SampleCache(in actualFormula);
		}

		protected override double[] ProcessSampleMetrics(Genome g, long sampleId)
		{
			var samples = Samples.Get(sampleId);
			var len = 10;
			var correct = new double[len];
			var divergence = new double[len];
			var calc = new double[len];
			var NaNcount = 0;

			// #if DEBUG
			// 			var gRed = g.AsReduced();
			// #endif

			for (var i = 0; i < len; i++)
			{
				var sample = samples[i];
				var s = sample.ParamValues;
				var correctValue = sample.Correct.Value;
				correct[i] = correctValue;
				var result = g.Evaluate(in s);
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
				divergence[i] = -Math.Abs(result - correctValue) * 10; // Averages can get too small.
			}

			if (NaNcount != 0)
			{
				// We do not yet handle NaN values gracefully yet so avoid correlation.
				return new[] {
					NaNcount == len // All NaN basically = fail.  Don't waste time trying to correlate.
						? double.NegativeInfinity
						: -2,
					double.NegativeInfinity
				};
			}
			else
			{
				var c = correct.Correlation(calc);
				if (c > 1) c = 1; // Must clamp double precision insanity.
				else if (c.IsPreciseEqual(1)) c = 1; // Compensate for epsilon.
													 //if (c > 1) c = 3 - 2 * c; // Correlation compensation for double precision insanity.
				var d = divergence.Where(v => !double.IsNaN(v)).Average() + 1;

				return new[] {
					(double.IsNaN(c) || double.IsInfinity(c)) ? -2 : c,
					(double.IsNaN(d) || double.IsInfinity(d)) ? double.NegativeInfinity : d
				};
			}
		}

	}


}
