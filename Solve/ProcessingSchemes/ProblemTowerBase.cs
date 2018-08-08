using System;
using System.Diagnostics.Contracts;

namespace Solve.ProcessingSchemes
{
	public abstract class ProblemTowerBase<TGenome> : BroadcasterBase<(TGenome Genome, int PoolIndex, Fitness)>, IGenomeProcessor<TGenome>
		where TGenome : class, IGenome
	{
		public readonly TowerProcessingSchemeBase<TGenome> Environment;
		public readonly IProblem<TGenome> Problem;

		protected ProblemTowerBase(
			IProblem<TGenome> problem,
			TowerProcessingSchemeBase<TGenome> environment)
		{
			Problem = problem ?? throw new ArgumentNullException(nameof(problem));
			Environment = environment ?? throw new ArgumentNullException(nameof(environment));
			Contract.EndContractBlock();

			this.Subscribe(champion => Environment.Broadcast((Problem, champion)));
		}

		public abstract void Post(TGenome next);

		public void Broadcast((TGenome Genome, Fitness[] Fitnesses) gf, int poolIndex)
			=> Broadcast((gf.Genome, poolIndex, gf.Fitnesses[poolIndex]));
	}
}
