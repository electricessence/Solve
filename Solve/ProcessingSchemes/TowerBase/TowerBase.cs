using System;
using System.Diagnostics.Contracts;

namespace Solve.ProcessingSchemes
{
	public abstract class TowerBase<TGenome, TEnvironment> : BroadcasterBase<(TGenome Genome, int PoolIndex, Fitness)>
		where TGenome : class, IGenome
		where TEnvironment : EnvironmentBase<TGenome>
	{
		public readonly TEnvironment Environment;
		public readonly IProblem<TGenome> Problem;

		protected TowerBase(
			IProblem<TGenome> problem,
			TEnvironment environment)
		{
			Problem = problem ?? throw new ArgumentNullException(nameof(problem));
			Environment = environment ?? throw new ArgumentNullException(nameof(environment));
			Contract.EndContractBlock();

			this.Subscribe(champion => Environment.Broadcast((Problem, champion)));
		}

		public void Broadcast((TGenome Genome, Fitness[] Fitnesses) gf, int poolIndex)
			=> Broadcast((gf.Genome, poolIndex, gf.Fitnesses[poolIndex]));
	}
}
