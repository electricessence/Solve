/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/Genetic-Algorithm-Platform/blob/master/LICENSE.md
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Open.Collections;
using Solve;
using System.Diagnostics;
using Open.Arithmetic;
using System.Collections.Concurrent;

namespace Eater
{


	public sealed class Problem : Solve.ProblemBase<EaterGenome>
	{
		public static readonly SampleCache Samples = new SampleCache();

		public Problem()
		{
		}

		protected override EaterGenome GetFitnessForKeyTransform(EaterGenome genome)
		{
			return genome;//.AsReduced(); // DO NOT measure against reduced because turns are expended energy and effect fitness.
		}


		protected override Task ProcessTest(EaterGenome g, Fitness fitness, long sampleId, bool useAsync = true)
		{
			return Task.Run(() => ProcessTestInternal(g, fitness, sampleId));
		}

		void ProcessTestInternal(EaterGenome g, Fitness fitness, long sampleId)
		{
			var boundary = Samples.Boundary;
			var samples = Samples.Get((int)sampleId);
			var len = 100;
			double found = 0;
			double energy = 0;

			for (var i = 0; i < len; i++)
			{
				var s = samples[i];
				int e;
				if (g.Try(boundary, s.EaterStart, s.Food, out e))
				{
					found++;
					Debug.Assert(g.AsReduced().Try(boundary, s.EaterStart, s.Food), "Reduced version should match.");
				}
				else
				{
					Debug.Assert(!g.AsReduced().Try(boundary, s.EaterStart, s.Food), "Reduced version should match.");
				}

				energy += e;
			}

			Debug.Assert(g.Hash.Length != 0 || found == 0, "An empty has should yield no results.");

			var ave = energy / len;
			var hlen = g.Hash.Length;

			fitness.AddScores(found / len, -ave, -hlen);// - Math.Pow(ave, 2) - hlen, ave, -hlen); // Adding the hash length seems superfluous but ends up being considered in the Pareto front.
		}

		static readonly ConcurrentDictionary<string, ProcedureResult> FullTests = new ConcurrentDictionary<string, ProcedureResult>();

		public static double LastScore = double.MaxValue;

		public static void EmitTopGenomeFullStats(KeyValuePair<IProblem<EaterGenome>, EaterGenome> kvp)
		{
			var p = kvp.Key;
			var genome = kvp.Value;
			var fitness = p.GetFitnessFor(genome).Value.Fitness;
			var result = FullTests.GetOrAdd(genome.Hash, key => Samples.TestAll(key));

			var asReduced = genome.AsReduced();
			if (asReduced == genome)
				Console.WriteLine("{0}:\t{1}", p.ID, genome.Hash);
			else
				Console.WriteLine("{0}:\t{1}\n=>\t{2}", p.ID, genome.Hash, asReduced.Hash);

			Console.WriteLine("  \t[{0}] ({1} samples)", fitness.Scores.JoinToString(","), fitness.SampleCount);
			Console.WriteLine("  \t[{0}] ({1} samples)", result.Average, result.Count);
			Console.WriteLine();

			if(LastScore>result.Average)
			{
				LastScore = result.Average;
				Console.WriteLine("New winner ^^^.");
				Console.ReadKey();
			}
		}

		public static void EmitTopGenomeStats(KeyValuePair<IProblem<EaterGenome>, EaterGenome> kvp)
		{
			var p = kvp.Key;
			var genome = kvp.Value;
			var fitness = p.GetFitnessFor(genome).Value.Fitness;

			var asReduced = genome.AsReduced();
			if (asReduced == genome)
				Console.WriteLine("{0}:\t{1}", p.ID, genome.Hash);
			else
				Console.WriteLine("{0}:\t{1}\n=>\t{2}", p.ID, genome.Hash, asReduced.Hash);

			Console.WriteLine("  \t[{0}] ({1} samples)", fitness.Scores.JoinToString(","), fitness.SampleCount);
			Console.WriteLine();
		}

	}



}