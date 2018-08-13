using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;

namespace Solve.ProcessingSchemes.Pull
{
	// ReSharper disable once PossibleInfiniteInheritance
	[SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
	public sealed partial class PullProcessingScheme<TGenome>
	{
		sealed class Level
		{
			LinkedListNode<Level> _levelNode;
			public Level NextLevel => _levelNode.Next?.Value;

			public Level(
				int level,
				ProblemTower tower,
				LinkedListNode<Level> node)
			{
				_levelNode = node ?? throw new ArgumentNullException(nameof(node));
				Contract.EndContractBlock();
			}

		}

	}
}
