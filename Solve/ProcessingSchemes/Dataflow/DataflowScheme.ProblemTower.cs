using System;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Solve.ProcessingSchemes.Dataflow
{
	// ReSharper disable once PossibleInfiniteInheritance
	public partial class DataflowScheme<TGenome>
	{
		sealed class ProblemTower : ProblemTowerBase<TGenome>
		{
			readonly Level Root;

			public ProblemTower(
				IProblem<TGenome> problem,
				TowerProcessingSchemeBase<TGenome> environment)
				: base(problem, environment)
			{
				Root = new Level(0, this, 1);
			}

			public override void Post(TGenome next)
			{
				if (next == null) throw new ArgumentNullException(nameof(next));
				Contract.EndContractBlock();

				Root.Post(0, (next, Problem.Pools.Select(f => new Fitness(f.Metrics)).ToArray()));
			}
		}
	}
}
