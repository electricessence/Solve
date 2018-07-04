using System;
using System.Linq;

namespace Solve.ProcessingSchemes
{
	public sealed partial class TowerProcessingScheme<TGenome>
	{
		public sealed class ProblemTower : BroadcasterBase<(TGenome Genome, FitnessContainer[])>, IGenomeProcessor<TGenome>
		{
			internal readonly TowerProcessingScheme<TGenome> Environment;
			internal readonly IProblem<TGenome> Problem;
			readonly Level Root;

			public ProblemTower(
				IProblem<TGenome> problem,
				TowerProcessingScheme<TGenome> environment) : base()
			{
				Problem = problem ?? throw new ArgumentNullException(nameof(problem));
				Environment = environment ?? throw new ArgumentNullException(nameof(environment));
				Root = new Level(0, this);
				this.Subscribe(champion => environment.Broadcast((Problem, champion)));
			}

			public void Post(TGenome next,
				bool express,
				bool expressToTop = false)
				=> Root.Post(
					(next, Problem.Pools.Select(f => new FitnessContainer(f.Metrics)).ToArray()),
					express,
					expressToTop);

			public void Post(TGenome next)
				=> Post(next, false);

		}
	}

}
