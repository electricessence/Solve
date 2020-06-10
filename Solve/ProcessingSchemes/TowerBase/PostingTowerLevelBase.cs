using Open.Memory;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	public abstract class PostingTowerLevelBase<TGenome, TTower> : TowerLevelBase<TGenome, TTower, TowerProcessingSchemeBase<TGenome>>
		where TGenome : class, IGenome
		where TTower : TowerBase<TGenome, TowerProcessingSchemeBase<TGenome>>
	{
		protected readonly IGenomeFactoryPriorityQueue<TGenome> Factory;
		protected abstract bool IsTop { get; }

		protected readonly bool IsMax;

		protected PostingTowerLevelBase(
			int level,
			TTower tower,
			byte priorityLevels)
			: base(level, tower.Environment.Config.PoolSize, tower)
		{
			var config = tower.Environment.Config;
			var max = config.MaxLevels;
			if (level > max) throw new ArgumentOutOfRangeException(nameof(level), level, $"Must be below maximum of {max}.");
			IsMax = level + 1 == config.MaxLevels;
			Factory = tower.Environment.Factory[1]; // Use a lower priority than the factory used by broadcasting.

			BestProgressiveFitness = new double[tower.Problem.Pools.Count][];

			Incomming = Enumerable
				.Range(0, priorityLevels)
				.Select(i => new ConcurrentQueue<LevelProgress<TGenome>>())
				.ToArray();
		}

		protected abstract ValueTask PostNextLevelAsync(byte priority, LevelProgress<TGenome> challenger);

		protected abstract ValueTask PostThisLevelAsync(LevelEntry<TGenome> entry);

		protected ValueTask PromoteAsync(byte priority, LevelEntry<TGenome> challenger)
		{
			if (IsMax) return PostThisLevelAsync(challenger);

			try
			{
				return PostNextLevelAsync(priority, challenger.Progress);
			}
			finally
			{
				LevelEntry<TGenome>.Pool.Give(challenger);
			}
		}

		protected readonly ConcurrentQueue<LevelProgress<TGenome>>[] Incomming;

		public async ValueTask PostAsync(byte priority, LevelProgress<TGenome> challenger)
		{
			Incomming[priority]
				.Enqueue(challenger);

			await OnAfterPost().ConfigureAwait(false);
		}

		protected abstract ValueTask ProcessInjested(byte priority, LevelProgress<TGenome> challenger);

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

		readonly double[][] BestProgressiveFitness;

		protected void ProcessChampion(int poolIndex, LevelProgress<TGenome> champ)
		{
			Debug.Assert(poolIndex >= 0);
			Tower.Problem.Pools[poolIndex].Champions?.Add(champ.Genome, champ.Fitnesses[poolIndex]);
			Tower.Broadcast(champ, poolIndex);
		}

		protected override async ValueTask<LevelEntry<TGenome>?> ProcessEntry(LevelProgress<TGenome> champ)
		{
			var result = (await Tower.Problem.ProcessSampleAsync(champ.Genome, Index).ConfigureAwait(false)).Select((fitness, i) =>
			{
				var values = fitness.Results.Sum;
				var progressiveFitness = champ.Fitnesses[i];
				var (success, fresh) = UpdateFitnessesIfBetter(
					BestProgressiveFitness,
					progressiveFitness
						.Merge(values)
						.Average
						.AsSpan(), i);

				Debug.Assert(progressiveFitness.MetricAverages.All(ma => ma.Value <= ma.Metric.MaxValue));

				return (values, success, fresh);
			}).ToArray();

			if (IsTop)
			{
				for (byte i = 0; i < result.Length; i++)
				{
					if (result[i].success)
						ProcessChampion(i, champ);
				}
			}

			if (IsMax || result.Any(r => r.fresh) || !result.Any(r => r.success))
				return LevelEntry<TGenome>.Init(in champ, result.Select(r => r.values).ToArray(), champ.Losses[Index]);

			Factory.EnqueueChampion(champ.Genome);
			await PostNextLevelAsync(0, champ).ConfigureAwait(false);
			return null;
		}

		protected async ValueTask ProcessSelection(LevelEntry<TGenome>[][] pools)
		{
			var poolCount = Tower.Problem.Pools.Count;
			Debug.Assert(poolCount != 0);
			var midPoint = PoolSize / 2;
			var processed = new HashSet<string>();

			var isTop = IsTop;
			// Remaining top 50% (winners) should go before any losers.
			for (var i = 0; i < midPoint; ++i)
			{
				for (var p = 0; p < poolCount; ++p)
				{
					var e = pools[p][i];
					var progress = e.Progress;
					var hash = progress.Genome.Hash;
					if (!processed.Add(hash)) continue;
					if (i == 0)
					{
						if (isTop) ProcessChampion(p, progress);
						await PromoteAsync(1, e).ConfigureAwait(false);
					}
					else
					{
						await PromoteAsync(2, e).ConfigureAwait(false);
					}
				}
			}

			// Distribute losers either back into the pool, pass them to the next level, or let them disapear (rejected).
			var config = Tower.Environment.Config;
			var maxLoses = config.MaxLevelLoss;
			var maxRejection = config.MaxConsecutiveRejections;
			var rejectionLimit = 95 * Index;
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
						if (lossRecord.ConcecutiveRejection > maxRejection || Index > 50 && 100 * totalRejections > rejectionLimit)
						{
							LevelEntry<TGenome>.Pool.Give(loser);
							//if (Tower.Environment.Factory is GenomeFactoryBase<TGenome> f)
							//	f.MetricsCounter.Increment("Genome Rejected");
						}
						else
						{
							await PromoteAsync(3, loser).ConfigureAwait(false);
						}
					}
					else
					{
						Debug.Assert(loser.LossCount == lossCount, $"LossCount: {loser.LossCount}, expected {lossCount}.");
						await PostThisLevelAsync(loser);
					}

				}
			}

			var arrayPool = ArrayPool<LevelEntry<TGenome>>.Shared;
			for (var i = 0; i < poolCount; i++) arrayPool.Return(pools[i]);
		}
	}
}
