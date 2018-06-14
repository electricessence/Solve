﻿using Open.Numeric;
using Solve;
using System.Collections.Generic;

namespace Eater
{


	public abstract class EaterProblem : ProblemBase<EaterGenome>
	{
		public readonly SampleCache Samples;

		protected EaterProblem(int gridSize = 10)
		{
			Samples = new SampleCache(gridSize);
		}

		protected override EaterGenome GetFitnessForKeyTransform(EaterGenome genome)
		{
			return genome;//.AsReduced(); // DO NOT measure against reduced because turns are expended energy and effect fitness.
		}

		public override IReadOnlyList<string> FitnessLabels { get; }
			= (new List<string> { "Food-Found-Rate {0:p}", "Average-Energy {0:n3}", "Gene-Count {0:n0}" }).AsReadOnly();

	}

	public sealed class EaterProblemFragmented : EaterProblem
	{
		public EaterProblemFragmented(int gridSize = 10) : base(gridSize)
		{
		}

		protected override void ProcessTest(EaterGenome g, Fitness fitness, long sampleId)
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
					//Debug.Assert(g.AsReduced().Try(boundary, s.EaterStart, s.Food), "Reduced version should match.");
				}
				else
				{
					//Debug.Assert(!g.AsReduced().Try(boundary, s.EaterStart, s.Food), "Reduced version should match.");
				}

				energy += e;
			}

			//Debug.Assert(g.Hash.Length != 0 || found == 0, "An empty has should yield no results.");

			var ave = energy / len;
			var geneCount = g.Genes.Count;
			fitness.AddScores(found / len, -ave, -geneCount);// - Math.Pow(ave, 2) - geneCount, ave, -geneCount); // Adding the geneCount seems superfluous but ends up being considered in the Pareto front.
		}

	}


	public sealed class ProblemFullTest : EaterProblem
	{
		public ProblemFullTest(int gridSize = 10) : base(gridSize)
		{
		}

		protected override void ProcessTest(EaterGenome g, Fitness fitness, long sampleId)
		{
			var fullTest = Samples.TestAll(g.Hash);
			var count = fullTest[0].Count;
			fitness.Add(fullTest[0]);
			fitness.Add(fullTest[1]);
			fitness.Add(new ProcedureResult(g.Genes.Count * count, count));
		}

	}
}
