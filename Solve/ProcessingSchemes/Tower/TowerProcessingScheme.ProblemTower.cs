using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	// ReSharper disable once PossibleInfiniteInheritance
	public sealed partial class TowerProcessingScheme<TGenome>
	{
		public sealed class ProblemTower : BroadcasterBase<(TGenome Genome, int PoolIndex, Fitness)>, IGenomeProcessor<TGenome>
		{
			internal readonly TowerProcessingScheme<TGenome> Environment;
			internal readonly IProblem<TGenome> Problem;
			readonly Level Root;

			public ProblemTower(
				IProblem<TGenome> problem,
				TowerProcessingScheme<TGenome> environment)
			{
				Problem = problem ?? throw new ArgumentNullException(nameof(problem));
				Environment = environment ?? throw new ArgumentNullException(nameof(environment));
				Contract.EndContractBlock();

				Root = new Level(0, this);
				this.Subscribe(champion => Environment.Broadcast((Problem, champion)));
			}

			public void Post(TGenome next,
				bool express,
				bool expressToTop = false)
			{
				if (next == null) throw new ArgumentNullException(nameof(next));
				Contract.EndContractBlock();

				Root.Post(
					(next, Problem.Pools.Select(f => new Fitness(f.Metrics)).ToArray()),
					express,
					expressToTop,
					true);
			}

			public Task PostAsync(TGenome next,
				CancellationToken token,
				bool express,
				bool expressToTop = false)
			{
				if (next == null) throw new ArgumentNullException(nameof(next));
				Contract.EndContractBlock();

				return Root.PostAsync(
					(next, Problem.Pools.Select(f => new Fitness(f.Metrics)).ToArray()),
					token,
					express,
					expressToTop,
					true);
			}

			public void Post(TGenome next)
				=> Post(next, false);

			public Task PostAsync(TGenome next, CancellationToken token)
				=> PostAsync(next, token, false);

			public void Broadcast((TGenome Genome, Fitness[] Fitnesses) gf, int index)
				=> Broadcast((gf.Genome, index, gf.Fitnesses[index]));
		}
	}

}
