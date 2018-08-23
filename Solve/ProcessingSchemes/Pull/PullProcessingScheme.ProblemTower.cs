using Open.Collections.Synchronized;
using System.Collections.Generic;

namespace Solve.ProcessingSchemes.Pull
{
	// ReSharper disable once PossibleInfiniteInheritance
	public sealed partial class PullProcessingScheme<TGenome>
	{
		sealed class ProblemTower : TowerBase<TGenome, PullProcessingScheme<TGenome>>
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

			public ProblemTower(
				IProblem<TGenome> problem,
				PullProcessingScheme<TGenome> environment)
				: base(problem, environment)
			{
				Levels = new LockSynchronizedLinkedList<Level>();
				Root = NewTopLevel();
			}

		}
	}

}
