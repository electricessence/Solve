using Solve;
using System.Collections.Generic;

namespace Eater
{


	public abstract class EaterProblem : ProblemBase<EaterGenome>
	{
		public readonly SampleCache Samples;

		protected EaterProblem(int gridSize = 10, ushort championPoolSize = 100)
			: base(championPoolSize, (Metrics01, Fitness01), (Metrics02, Fitness02))
		{
			Samples = new SampleCache(gridSize);
		}

		static FitnessContainer Fitness01(EaterGenome genome, double[] metrics)
			=> new FitnessContainer(Metrics01, metrics[0], -metrics[1], -genome.Length);

		static FitnessContainer Fitness02(EaterGenome genome, double[] metrics)
			=> new FitnessContainer(Metrics02, metrics[0], -genome.Length, -metrics[1]);

		protected static readonly IReadOnlyList<Metric> Metrics01 = new List<Metric>
		{
			new Metric(0, "Food-Found-Rate", "Food-Found-Rate {0:p}"),
			new Metric(1, "Average-Energy", "Food-Found-Rate {0:p}"),
			new Metric(2, "Gene-Count", "Gene-Count {0:n0}")
		}.AsReadOnly();

		protected static readonly IReadOnlyList<Metric> Metrics02 = new List<Metric>
		{
			Metrics01[0],
			Metrics01[2],
			Metrics01[1]
		}.AsReadOnly();

	}

	public sealed class EaterProblemFragmented : EaterProblem
	{
		public EaterProblemFragmented(int gridSize = 10, int sampleSize = 100) : base(gridSize)
		{
			SampleSize = sampleSize;
		}

		public readonly int SampleSize;

		protected override double[] ProcessSampleMetrics(EaterGenome g, long sampleId)
		{
			var boundary = Samples.Boundary;
			var samples = Samples.Get((int)sampleId);
			var len = SampleSize;
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
			return new[] {
				found / len,
				ave
			};// - Math.Pow(ave, 2) - geneCount, ave, -geneCount); // Adding the geneCount seems superfluous but ends up being considered in the Pareto front.
		}

	}

}
