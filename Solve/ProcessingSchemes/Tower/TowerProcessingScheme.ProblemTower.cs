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
		public sealed class ProblemTower : ProblemTowerBase<TGenome>
		{
			readonly Level Root;

			public ProblemTower(
				IProblem<TGenome> problem,
				// ReSharper disable once SuggestBaseTypeForParameter
				TowerProcessingScheme<TGenome> environment)
				: base(problem, environment)
			{
				Root = new Level(0, this);
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

			public override void Post(TGenome next)
				=> Post(next, false);

			public override Task PostAsync(TGenome next, CancellationToken token)
				=> PostAsync(next, token, false);
		}
	}

}
