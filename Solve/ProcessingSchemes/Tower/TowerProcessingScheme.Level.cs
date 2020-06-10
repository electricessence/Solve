using Open.ChannelExtensions;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes.Tower
{
	// ReSharper disable once PossibleInfiniteInheritance
	[SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
	public sealed partial class TowerProcessingScheme<TGenome>
	{
		sealed class Level : PostingTowerLevelBase<TGenome, ProblemTower>
		{
			Level? _nextLevel;
			public Level NextLevel => LazyInitializer.EnsureInitialized(ref _nextLevel,
				() => new Level(Index + 1, Tower));

			protected override bool IsTop => _nextLevel == null;

			private readonly Channel<LevelEntry<TGenome>> Pool;
			private readonly ChannelReader<List<LevelEntry<TGenome>>> PoolReader;

			public Level(
				int level,
				ProblemTower tower,
				byte priorityLevels = 4)
				: base(level, tower, priorityLevels)
			{
				Console.WriteLine("Level Created: {0}", level);

				Pool = Channel.CreateUnbounded<LevelEntry<TGenome>>(new UnboundedChannelOptions
				{
					AllowSynchronousContinuations = true
				});

				Processed = Enumerable
					.Range(0, priorityLevels)
					.Select(i => new ConcurrentQueue<LevelEntry<TGenome>>())
					.ToArray();

				PoolReader = Pool.Reader.Batch(PoolSize, false, true);
				PoolReader.ReadAllAsync(ProcessPoolAsyncInternal);
			}

			readonly ConcurrentQueue<LevelEntry<TGenome>>[] Processed;

			async ValueTask ProcessPoolAsyncInternal(List<LevelEntry<TGenome>> pool)
			{
				var selection = RankEntries(pool);
				await ProcessSelection(selection);
				ArrayPool<LevelEntry<TGenome>[]>.Shared.Return(selection, true);

			}

			public async ValueTask ProcessPoolAsync(bool thisLevelOnly = false)
			{
				while (PoolReader.TryRead(out var pool))
				{
					await ProcessPoolAsyncInternal(pool).ConfigureAwait(false);
				}

				if (thisLevelOnly) return;

				// walk up instead of recurse.
				Level? next = this;
				while ((next = next._nextLevel) != null)
					await next.ProcessPoolAsync(true).ConfigureAwait(false);
			}

			protected override async ValueTask OnAfterPost()
			{
				await base.OnAfterPost().ConfigureAwait(false);

				int count;
				do
				{
					count = 0;
					var len = Processed.Length;
					for (var i = 0; i < len; i++)
					{
						var q = Processed[i];
						if (!q.TryDequeue(out var p)) continue;

						await PostThisLevelAsync(p).ConfigureAwait(false);

						i = -1; // Reset to top queue.
						++count;
					}
				}
				while (count != 0);
			}

			protected override ValueTask PostNextLevelAsync(byte priority, LevelProgress<TGenome> challenger)
				=> NextLevel.PostAsync(priority, challenger);

			protected override ValueTask PostThisLevelAsync(LevelEntry<TGenome> entry)
				=> Pool.Writer.WriteAsync(entry);

			void ProcessInjested(byte priority, LevelEntry<TGenome>? challenger)
			{
				if (challenger != null)
					Processed[priority].Enqueue(challenger);
			}

			async ValueTask ProcessInjestedAsync(byte priority, ValueTask<LevelEntry<TGenome>?> challenger)
				=> ProcessInjested(priority, await challenger.ConfigureAwait(false));

			protected override ValueTask ProcessInjested(byte priority, LevelProgress<TGenome> challenger)
				=> ProcessInjestedAsync(priority, ProcessEntry(challenger));
		}

	}
}
