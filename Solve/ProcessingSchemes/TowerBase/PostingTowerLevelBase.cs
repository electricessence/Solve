using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
    public abstract class PostingTowerLevelBase<TGenome, TTower> : ReceivingTowerLevelBase<TGenome, TTower>
		where TGenome : class, IGenome
		where TTower : TowerBase<TGenome, EnvironmentBase<TGenome>>
	{
		protected PostingTowerLevelBase(
			int level,
			TTower tower,
			SchemeConfig config,
			byte priorityLevels)
			: base(level, config, tower)
		{
			Incomming = Enumerable
				.Range(0, priorityLevels)
				.Select(i => new ConcurrentQueue<LevelProgress<TGenome>>())
				.ToImmutableArray();
		}

		protected readonly ImmutableArray<ConcurrentQueue<LevelProgress<TGenome>>> Incomming;

		public async ValueTask PostAsync(byte priority, LevelProgress<TGenome> challenger)
		{
			Incomming[priority]
				.Enqueue(challenger);

			await OnAfterPost().ConfigureAwait(false);
		}

		protected virtual async ValueTask OnAfterPost()
		{
			int count;
			do
			{
				count = 0;
				var len = Incomming.Length;
				for (byte i = 0; i < len; i++)
				{
				retry:
					var q = Incomming[i];
					if (!q.TryDequeue(out var c)) continue;

					await ProcessInjested(i, c).ConfigureAwait(false);

					i = 0; // Reset to top queue.
					++count;

					goto retry;
				}
			}
			while (count != 0);
		}

	}
}
