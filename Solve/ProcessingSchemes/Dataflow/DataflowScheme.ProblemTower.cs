using Solve.Supporting.TaskScheduling;
using System;
using System.Diagnostics.Contracts;

namespace Solve.ProcessingSchemes.Dataflow
{
	// ReSharper disable once PossibleInfiniteInheritance
	public partial class DataflowScheme<TGenome>
	{
		sealed class ProblemTower : TowerBase<TGenome, TowerProcessingSchemeBase<TGenome>>
		{
			readonly Level Root;

			public readonly PriorityQueueTaskScheduler Scheduler;

			public ProblemTower(
				IProblem<TGenome> problem,
				TowerProcessingSchemeBase<TGenome> environment)
				: base(problem, environment)
			{
				Scheduler = environment.Scheduler[0];
				Scheduler.Name = "ProblemTower Scheduler";
				Scheduler.ReversePriority = true; // The top level should go first...

				Root = new Level(0, this, 1);
			}

			public void Post(TGenome next)
			{
				if (next is null) throw new ArgumentNullException(nameof(next));
				Contract.EndContractBlock();

				Root.Post(0, (next, NewFitness()));
			}
		}
	}
}
