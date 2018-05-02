/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/Genetic-Algorithm-Platform/blob/master/LICENSE.md
 */

using Open.Numeric;
using Solve;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Eater
{


	public abstract class Problem : Solve.ProblemBase<EaterGenome>
	{
		public readonly SampleCache Samples;

		protected Problem(int gridSize = 10)
		{
			Samples = new SampleCache();
		}

		protected override EaterGenome GetFitnessForKeyTransform(EaterGenome genome)
		{
			return genome;//.AsReduced(); // DO NOT measure against reduced because turns are expended energy and effect fitness.
		}


		protected override Task ProcessTest(EaterGenome g, Fitness fitness, long sampleId, bool useAsync = true)
		{
			return Task.Run(() => ProcessTestInternal(g, fitness, sampleId));
		}

		protected abstract void ProcessTestInternal(EaterGenome g, Fitness fitness, long sampleId);		
	}

	public sealed class ProblemFragmented : Problem
	{
		public ProblemFragmented(int gridSize = 10):base(gridSize)
		{
		}
		protected override void ProcessTestInternal(EaterGenome g, Fitness fitness, long sampleId)
		{

			var boundary = Samples.Boundary;
			var samples = Samples.Get((int)sampleId);
			var len = 100;
			double found = 0;
			double energy = 0;

			for (var i = 0; i < len; i++)
			{
				var s = samples[i];
				if (g.Try(boundary, s.EaterStart, s.Food, out int e))
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
			var hlen = g.AsReduced().Hash.Length;
			fitness.AddScores(found / len, -ave, -hlen);// - Math.Pow(ave, 2) - hlen, ave, -hlen); // Adding the hash length seems superfluous but ends up being considered in the Pareto front.
		}

		public static readonly IReadOnlyList<string> FitnessLabels
			= (new List<string> { "Food-Found-Rate {0:p}", "Average-Energy {0:n3}", "Hash-Length {0:n0}" }).AsReadOnly();
	}


	public sealed class ProblemFullTest : Problem
	{
		public ProblemFullTest(int gridSize = 10) : base(gridSize)
		{
		}

		protected override void ProcessTestInternal(EaterGenome g, Fitness fitness, long sampleId)
		{
			var fullTest = Samples.TestAll(g.Hash);
			var count = fullTest[0].Count;
			fitness.Add(fullTest[0]);
			fitness.Add(fullTest[1]);
			fitness.Add(new ProcedureResult(g.Hash.Length * count, count));
		}

	}
}