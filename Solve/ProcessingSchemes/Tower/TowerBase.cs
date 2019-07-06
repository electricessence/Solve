using System;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Solve.ProcessingSchemes.Tower
{
	public abstract partial class TowerProcessingSchemeBase<TGenome, TTower, TEnvironment> : EnvironmentBase<TGenome>
	{
		public abstract partial class TowerBase : BroadcasterBase<(TGenome Genome, int PoolIndex, Fitness)>
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
}
