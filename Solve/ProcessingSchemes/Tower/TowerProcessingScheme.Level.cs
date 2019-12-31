using Open.ChannelExtensions;
using Open.Collections;
using Open.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
				Pool = Channel.CreateUnbounded<LevelEntry<TGenome>>();
				
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
				// 1) Setup selection.
				var len = pool.Count;
				var midPoint = pool.Count / 2;

				var promoted = new HashSet<string>();

				var problemPools = Tower.Problem.Pools;
				var problemPoolCount = problemPools.Count;
				var selection = RankEntries(pool);
				var isTop = _nextLevel == null;
				for (var i = 0; i < problemPoolCount; i++)
				{
					var s = selection[i];

					// 2) Signal & promote champions.
					var champ = s[0].GenomeFitness;

					if (isTop)
						ProcessChampion(i, champ);

					if (promoted.Add(champ.Genome.Hash))
						await PostNextLevelAsync(1, champ); // Champs may need to be posted synchronously to stay ahead of other deferred winners.

					// 3) Increment fitness rejection for individual fitnesses.
					for (var n = midPoint; n < len; n++)
					{
						s[n].GenomeFitness.Fitness[i].IncrementRejection();
					}
				}

				// 4) Promote remaining winners (weaving to ensure that other threads honor the priority)
				for (var n = 1; n < midPoint; n++)
				{
					for (var i = 0; i < problemPoolCount; i++)
					{
						var s = selection[i];
						var winner = s[n].GenomeFitness;
						if (promoted.Add(winner.Genome.Hash))
							await PostNextLevelAsync(2, winner); // PostStandby?
					}
				}

				//NextLevel.ProcessPool(true); // Prioritize winners and express.

				// 5) Process remaining (losers)
				var maxLoses = Tower.Environment.MaxLevelLosses;
				var maxRejection = Tower.Environment.MaxLossesBeforeElimination;
				foreach (var remainder in selection.Cast<IEnumerable<LevelEntry<TGenome>>>().Weave().Distinct())
				{
					if (promoted.Contains(remainder.GenomeFitness.Genome.Hash))
					{
						LevelEntry<TGenome>.Pool.Give(remainder);
						continue;
					}

					remainder.IncrementLoss();

					if (remainder.LevelLossRecord > maxLoses)
					{
						var gf = remainder.GenomeFitness;
						var fitnesses = gf.Fitness;
						if (fitnesses.Any(f => f.RejectionCount < maxRejection))
							await PostNextLevelAsync(3, gf);
						else
						{
							//Host.Problem.Reject(loser.GenomeFitness.Genome.Hash);
							if (Tower.Environment.Factory is GenomeFactoryBase<TGenome> f)
								f.MetricsCounter.Increment("Genome Rejected");

							//#if DEBUG
							//							if (IsTrackedGenome(gf.Genome.Hash))
							//								Debugger.Break();
							//#endif

						}

						LevelEntry<TGenome>.Pool.Give(remainder);
					}
					else
					{
						Debug.Assert(remainder.LevelLossRecord > 0);
						await Pool.Writer.WriteAsync(remainder);// Didn't win, but still in the game.
					}
				}

				foreach (var sel in selection) sel.Dispose();
				selection.Dispose();
			}

			public async ValueTask ProcessPoolAsync(bool thisLevelOnly = false)
			{
				while (PoolReader.TryRead(out var pool))
				{
					await ProcessPoolAsyncInternal(pool);
				}

				if (thisLevelOnly) return;

				// walk up instead of recurse.
				Level? next = this;
				while ((next = next._nextLevel) != null)
					await next.ProcessPoolAsync(true);
			}

			protected override async ValueTask OnAfterPost()
			{
				await base.OnAfterPost();

				int count;
				do
				{
					count = 0;
					var len = Processed.Length;
					for (var i = 0; i < len; i++)
					{
						var q = Processed[i];
						if (!q.TryDequeue(out var p)) continue;

						await Pool.Writer.WriteAsync(p);

						i = -1; // Reset to top queue.
						++count;
					}
				}
				while (count != 0);
			}

			protected override ValueTask PostNextLevelAsync(byte priority, (TGenome Genome, Fitness[] Fitness) challenger)
				=> NextLevel.PostAsync(priority, challenger);

			void ProcessInjested(byte priority, LevelEntry<TGenome>? challenger)
			{
				if (challenger != null)
					Processed[priority].Enqueue(challenger);
			}

			async ValueTask ProcessInjestedAsync(byte priority, ValueTask<LevelEntry<TGenome>?> challenger)
				=> ProcessInjested(priority, await challenger);

			protected override ValueTask ProcessInjested(byte priority, (TGenome Genome, Fitness[] Fitness) challenger)
				=> ProcessInjestedAsync(priority, ProcessEntry(challenger));
		}

	}
}
