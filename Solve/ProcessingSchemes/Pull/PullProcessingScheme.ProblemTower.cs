using Open.Collections.Synchronized;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Solve.ProcessingSchemes.Pull
{
	// ReSharper disable once PossibleInfiniteInheritance
	public sealed partial class PullProcessingScheme<TGenome>
	{
		sealed class ProblemTower : BroadcasterBase<(TGenome Genome, int PoolIndex, Fitness)>
		{
			readonly Level Root;

			readonly LockSynchronizedLinkedList<Level> Levels;

			Level NewTopLevel(LinkedList<Level> list)
			{
				var count = list.Count;
				var node = list.AddLast(default(Level));
				return node.Value = new Level(count, this, node);
			}

			public Level GetNextLevel(Level level)
				=> level.NextLevel
					?? Levels.Modify(list =>
						level.NextLevel
						?? NewTopLevel(list));

			public Level NewTopLevel()
				=> Levels.Modify(NewTopLevel);

			public readonly PullProcessingScheme<TGenome> Environment;
			public readonly IProblem<TGenome> Problem;


			public ProblemTower(
				IProblem<TGenome> problem,
				PullProcessingScheme<TGenome> environment)
			{
				Problem = problem ?? throw new ArgumentNullException(nameof(problem));
				Environment = environment ?? throw new ArgumentNullException(nameof(environment));
				Contract.EndContractBlock();

				Levels = new LockSynchronizedLinkedList<Level>();
				Root = NewTopLevel();

				this.Subscribe(champion => Environment.Broadcast((Problem, champion)));
			}

		}
	}

}
