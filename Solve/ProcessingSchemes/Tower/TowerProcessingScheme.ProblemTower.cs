using System;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Solve.ProcessingSchemes.Tower
{
	// ReSharper disable once PossibleInfiniteInheritance
	public sealed partial class TowerProcessingScheme<TGenome>
	{
		public sealed class ProblemTower : TowerBase<TGenome, TowerProcessingSchemeBase<TGenome>>
		{
			readonly Level Root;

			public ProblemTower(
				IProblem<TGenome> problem,
				// ReSharper disable once SuggestBaseTypeForParameter
				TowerProcessingScheme<TGenome> environment)
				: base(problem, environment)
			{
				Root = new Level(0, this, 1);
			}

			public void Post(TGenome next)
			{
				if (next is null) throw new ArgumentNullException(nameof(next));
				Contract.EndContractBlock();

				Root.Post(0, (next, Problem.Pools.Select(f => new Fitness(f.Metrics)).ToArray()));
			}

			public void ProcessPools()
			{
				Root.ProcessPool();
			}


		}
	}

}
