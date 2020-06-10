using System;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;

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

		public void Broadcast(LevelProgress<TGenome> progress, int poolIndex)
			=> Broadcast((progress.Genome, poolIndex, progress.Fitnesses[poolIndex]));

		public ImmutableArray<Fitness> NewFitness()
			=> Problem.Pools.Select(f => new Fitness(f.Metrics)).ToImmutableArray();
	}
}
