/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/Genetic-Algorithm-Platform/blob/master/LICENSE.md
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Open.Arithmetic;
using Open.Collections;
using Open.Formatting;
using Solve;

namespace BlackBoxFunction
{

	public delegate double Formula(IReadOnlyList<double> p);

	///<summary>
	/// The 'Problem' class is important for tracking fitness results and deciding how well a genome is peforming.
	/// It's possible to have multiple 'problems' being measured at once.
	///</summary>
	public class Problem : Solve.ProblemBase<Genome>
	{
		public readonly SampleCache Samples;


		public Problem(Formula actualFormula)
		{
			Samples = new SampleCache(actualFormula);
		}

		protected override Genome GetFitnessForKeyTransform(Genome genome)
		{
			return genome.AsReduced();
		}

		protected override Task ProcessTest(Genome g, Fitness fitness, long sampleId, bool useAsync = true)
		{
			return Task.Run(() => ProcessTestInternal(g, fitness, sampleId));
		}

		void ProcessTestInternal(Genome g, Fitness fitness, long sampleId, bool useAsync = true)
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
				divergence[i] = -Math.Abs(result - correctValue) * 10; // Averages can get too small.
			}

			if (NaNcount != 0)
			{
				// We do not yet handle NaN values gracefully yet so avoid correlation.
				fitness.AddScores(
					NaNcount == len // All NaN basically = fail.  Don't waste time trying to correlate.
						? double.NegativeInfinity
						: -2,
					double.NegativeInfinity);
			}
			else
			{
				var c = correct.Correlation(calc);
				if (c > 1) c = 1; // Must clamp double precision insanity.
				else if (c.IsPreciseEqual(1)) c = 1; // Compensate for epsilon.
													 //if (c > 1) c = 3 - 2 * c; // Correlation compensation for double precision insanity.
				var d = divergence.Where(v => !double.IsNaN(v)).Average() + 1;

				fitness.AddScores(
					(double.IsNaN(c) || double.IsInfinity(c)) ? -2 : c,
					(double.IsNaN(d) || double.IsInfinity(d)) ? double.NegativeInfinity : d
				);
			}
		}

		public static void EmitTopGenomeStats(KeyValuePair<IProblem<Genome>, Genome> kvp)
		{
			var p = kvp.Key;
			var genome = kvp.Value;
			var fitness = p.GetFitnessFor(genome).Value.Fitness;

			var asReduced = genome.AsReduced();
			if (asReduced == genome)
				Console.WriteLine("{0}:\t{1}", p.ID, genome.ToAlphaParameters());
			else
				Console.WriteLine("{0}:\t{1}\n=>\t{2}", p.ID, genome.ToAlphaParameters(), asReduced.ToAlphaParameters());

			Console.WriteLine("  \t[{0}] ({1} samples)", fitness.Scores.JoinToString(","), fitness.SampleCount);
			Console.WriteLine();
		}

	}


}