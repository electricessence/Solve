using System;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Solve.ProcessingSchemes.Tower
{

	public abstract class TowerBase<TGenome, TEnvironment> : BroadcasterBase<(TGenome Genome, int PoolIndex, Fitness)>
		where TGenome : class, IGenome
		where TEnvironment : EnvironmentBase<TGenome>
	{
		public readonly TEnvironment Environment;
		public readonly IProblem<TGenome> Problem;
		readonly IDisposable Subscription;

		protected TowerBase(
			IProblem<TGenome> problem,
			TEnvironment environment)
		{
			Problem = problem ?? throw new ArgumentNullException(nameof(problem));
			Environment = environment ?? throw new ArgumentNullException(nameof(environment));
			Contract.EndContractBlock();

			Subscription = this.Subscribe(champion => Environment.Broadcast((Problem, champion)));
		}

		protected override void OnDispose()
		{
			base.OnDispose();
			Subscription.Dispose();
		}

		public void Broadcast((TGenome Genome, Fitness[] Fitnesses) gf, int poolIndex)
			=> Broadcast((gf.Genome, poolIndex, gf.Fitnesses[poolIndex]));

		public Fitness[] NewFitness()
			=> Problem.Pools.Select(f => new Fitness(f.Metrics)).ToArray();
	}

}
