using Open.ChannelExtensions;
using Open.Disposable;
using Open.Memory;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	public partial class TowerScheme<TGenome>
	{
		protected class Level : IAsyncLevel<TGenome>
		{
			protected bool IsTop => !_nextLevel.IsValueCreated;
			protected readonly bool IsMax;

			protected readonly int Index;
			protected readonly ushort PoolSize;
			protected readonly ProblemTower Tower;
			readonly Lazy<IAsyncLevel<TGenome>> _nextLevel;
			public IAsyncLevel<TGenome> NextLevel => _nextLevel.Value;

			protected readonly Memory<double[]> BestLevelFitness;
			protected readonly Memory<double[]> BestProgressiveFitness;

			protected readonly Channel<LevelEntry<TGenome>> Pool;

			protected virtual IAsyncLevel<TGenome> CreateNextLevel()
				=> new Level(Index + 1, Tower);

			public Level(
				int level,
				ProblemTower tower)
			{
				Debug.Assert(level >= 0);
				Debug.Assert(tower is not null);
				tower.OnLevelCreated(level);
				var config = tower.Config;
				Index = level;
				PoolSize = config.PoolSize.GetPoolSize(level);
				Tower = tower;

				var max = config.MaxLevels;
				if (level > max) throw new ArgumentOutOfRangeException(nameof(level), level, $"Must be below maximum of {max}.");
				IsMax = level + 1 == config.MaxLevels;

				_nextLevel = new(CreateNextLevel);

				var poolCount = tower.Problem.Pools.Count;
				BestLevelFitness = new double[poolCount][];
				BestProgressiveFitness = new double[poolCount][];

				Pool = Channel.CreateBounded<LevelEntry<TGenome>>(new BoundedChannelOptions(PoolSize)
				{
					SingleReader = true,
					SingleWriter = false,
					AllowSynchronousContinuations = true
				});

				var index = 0;
				var buffer = new LevelEntry<TGenome>[PoolSize];
				_ = Pool.Reader.ReadAllAsync(async e =>
				{
					buffer[index++] = e;
					if (index == PoolSize) index = await ProcessReceived(buffer);
				}).AsTask();
			}

			protected virtual ValueTask<int> ProcessReceived(LevelEntry<TGenome>[] fullBuffer)
				=> ProcessSelection(fullBuffer, RankEntries(fullBuffer));

			protected static (bool success, bool isFresh) UpdateFitnessesIfBetter(
				in Span<double[]> registry,
				in ReadOnlySpan<double> contending,
				int fitnessIndex)
			{
				ref var fRef = ref registry[fitnessIndex];
				double[]? defending;
				double[]? contendingArray = null;
				while ((defending = fRef) is null || contending.IsGreaterThan(defending.AsSpan()))
				{
					contendingArray ??= contending.ToArray();
					if (Interlocked.CompareExchange(ref fRef!, contendingArray, defending) == defending)
						return (true, defending is null);
				}
				return (false, false);
			}

			protected LevelEntry<TGenome>[][] RankEntries(IReadOnlyCollection<LevelEntry<TGenome>> pool)
			{
				var len = pool.Count;
				var poolCount = Tower.Problem.Pools.Count;
				var result = new LevelEntry<TGenome>[poolCount][];

				for (var i = 0; i < poolCount; i++)
				{
					var temp = pool.ToArray();
					result[i] = temp;
					var comparer = LevelEntry<TGenome>.GetScoreComparer(i);
					TrySorting(3);

					void TrySorting(int max)
					{
						var tries = 0;
						while (tries++ < max)
						{
							try
							{
								// Verify repeatable issue.
								Array.Sort(temp, 0, len, comparer);
								return;
							}
							catch (Exception ex)
							{
								if (tries == max)
								{
									Debug.WriteLine(ex.ToString());
									Debugger.Break();
									throw;
								}
							}
						}
					}
				}

				return result;
			}

			protected void ProcessChampion(int poolIndex, LevelProgress<TGenome> champ)
			{
				Debug.Assert(poolIndex >= 0);
				if (Index == 0) return; // Ignore champions from first level.
				Tower.Problem.Pools[poolIndex].Champions?.Add(champ.Genome, champ.Fitnesses[poolIndex]);
				Tower.Broadcast(champ, poolIndex);
			}

			public virtual async ValueTask PostAsync(LevelProgress<TGenome> contender)
			{
				var result = (await Tower.Problem.ProcessSampleAsync(contender.Genome, Index).ConfigureAwait(false))
					.Select((fitness, i) =>
					{
						var levelFitness = fitness.Results.Sum;
						var (levelWinner, isFirstofLevel) = UpdateFitnessesIfBetter(BestLevelFitness.Span, levelFitness.AsSpan(), i);

						var fitnessRecord = contender.Fitnesses[i];
						var fitnessRecordNew = fitnessRecord.Merge(levelFitness).Average.AsSpan();
						var (progressiveWinner, isFirstofProgressive) = UpdateFitnessesIfBetter(BestProgressiveFitness.Span, fitnessRecordNew, i);

						Debug.Assert(fitnessRecord.MetricAverages.All(ma => ma.Value <= ma.Metric.MaxValue));

						return (
							levelFitness,
							success: levelWinner || progressiveWinner,
							fresh: isFirstofLevel || isFirstofProgressive
						);
					}).ToArray();

				if (IsTop)
				{
					for (byte i = 0; i < result.Length; i++)
					{
						if (result[i].success) // Get the champion for each fitness.
							ProcessChampion(i, contender);
					}
				}

				if (IsMax || IsTop // Let the pools fill up first before fast-tracking new champions.
								   //|| result.Any(r => r.fresh)
					|| !result.Any(r => r.success))
				{
					await Pool.Writer
						.WriteAsync(LevelEntry<TGenome>.Init(in contender, result.Select(r => r.levelFitness).ToArray(), contender.Losses[Index]))
						.ConfigureAwait(false);
				}
				else
				{
					Tower.Factory[0]
						.EnqueueChampion(contender.Genome);

					await NextLevel
						.PostAsync(contender)
						.ConfigureAwait(false);
				}
			}

			protected ValueTask PromoteAsync(LevelEntry<TGenome> champion)
			{
				var progress = champion.Progress;
				LevelEntry<TGenome>.Pool.Give(champion);
				return NextLevel.PostAsync(progress);
			}

			protected async ValueTask<int> ProcessSelection(LevelEntry<TGenome>[] buffer, LevelEntry<TGenome>[][] pools)
			{
				var poolCount = Tower.Problem.Pools.Count;
				Debug.Assert(poolCount != 0);
				var midPoint = PoolSize / 2;
				var lPool = ListPool<LevelEntry<TGenome>>.Shared;
				var hsPool = HashSetPool<string>.Shared;
				var processed = hsPool.Take();
				var toPromote = lPool.Take();
				var toKill = lPool.Take();

				//var isTop = IsTop;
				// Remaining top 50% (winners) should go before any losers.
				for (var i = 0; i < midPoint; ++i)
				{
					for (var p = 0; p < poolCount; ++p)
					{
						var e = pools[p][i];
						var progress = e.Progress;
						var hash = progress.Genome.Hash;
						if (processed.Add(hash)) toPromote.Add(e);
					}
				}

				// Distribute losers either back into the pool, pass them to the next level, or let them disapear (rejected).
				var config = Tower.Config;
				var maxLoses = config.MaxLevelLoss;
				var maxRejection = config.MaxConsecutiveRejections;
				var percentRejectionLimit = config.PercentRejectedBeforeElimination;
				var retained = 0;
				for (var i = midPoint; i < PoolSize; ++i)
				{
					for (var p = 0; p < poolCount; ++p)
					{
						var loser = pools[p][i];
						var progress = loser.Progress;
						var hash = progress.Genome.Hash;
						if (!processed.Add(hash)) continue;

						var lossCount = loser.LossCount.Increment();
						if (lossCount > maxLoses)
						{
							var lossRecord = progress.Losses;
							var totalRejections = lossRecord.IncrementRejection(Index);

							if (lossRecord.ConcecutiveRejection > maxRejection
								&& 100 * totalRejections > Index * percentRejectionLimit)
							{
								// Permanently rejected.
								toKill.Add(loser);
							}
							else
							{
								// Survived for another try.
								toPromote.Add(loser);
							}
						}
						else
						{
							Debug.Assert(loser.LossCount == lossCount, $"LossCount: {loser.LossCount}, expected {lossCount}.");
							buffer[retained++] = loser;
						}

					}
				}

				hsPool.Give(processed);
				Array.Clear(buffer, retained, buffer.Length - retained);

#if DEBUG
				Debug.Assert(toPromote.Distinct().Count() == toPromote.Count);
#endif
				foreach (var p in toPromote) await PromoteAsync(p).ConfigureAwait(false);
				lPool.Give(toPromote);

				foreach (var k in toKill)
				{
					k.Progress.Dispose();
					LevelEntry<TGenome>.Pool.Give(k);
				}

				return retained;
			}
		}
	}
}
