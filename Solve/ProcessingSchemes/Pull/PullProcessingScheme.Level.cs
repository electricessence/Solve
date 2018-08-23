using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes.Pull
{
	// ReSharper disable once PossibleInfiniteInheritance
	[SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
	public sealed partial class PullProcessingScheme<TGenome>
	{
		sealed class Level : TowerLevelBase<TGenome, ProblemTower, PullProcessingScheme<TGenome>>
		{
			LinkedListNode<Level> _levelNode;
			public Level NextLevel => _levelNode.Next?.Value;
			readonly Level PreviousLevel;

			public Level(
				int level,
				ProblemTower tower,
				LinkedListNode<Level> node)
				: base(level, tower?.Environment.PoolSize ?? throw new ArgumentNullException(nameof(tower)), tower)
			{
				_levelNode = node ?? throw new ArgumentNullException(nameof(node));
				Contract.EndContractBlock();

				PreviousLevel = node.Previous?.Value;
			}

			//Channel Processing = Channel.CreateUnbounded < ()

			//void PostResult()

			public async Task<(TGenome Genome, Fitness[] Fitness)> GetNextAsync()
			{
				var pool = new List<(TGenome Genome, Fitness[])>(PoolSize);
				if (PreviousLevel != null)
				{
					while (pool.Count < PoolSize)
					{
						pool.Add(await PreviousLevel.GetNextAsync());
					}
				}
				else
				{
					Tower.Environment.Factory.Take(PoolSize)
						.Select(g => Tower.Problem.ProcessSampleAsync(g, Index));
				}
				throw new NotImplementedException();
			}

			protected override Task<LevelEntry> ProcessEntry((TGenome Genome, Fitness[] Fitness) champ)
			{
				throw new NotImplementedException();
			}
		}

	}
}
