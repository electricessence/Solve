using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
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

		protected PostingTowerLevelBase(
			int level,
			TTower tower,
			byte priorityLevels)
			: base(level, tower?.Environment.PoolSize ?? throw new ArgumentNullException(nameof(tower)), tower)
		{
			Factory = tower.Environment.Factory[1]; // Use a lower priority than the factory used by broadcasting.

			BestProgressiveFitness = new double[tower.Problem.Pools.Count][];

			Incomming = Enumerable
				.Range(0, priorityLevels)
				.Select(i => new ConcurrentQueue<(TGenome Genome, Fitness[] Fitness)>())
				.ToArray();
		}

		protected abstract ValueTask PostNextLevelAsync(byte priority, (TGenome Genome, Fitness[] Fitness) challenger);

		protected readonly ConcurrentQueue<(TGenome Genome, Fitness[] Fitness)>[] Incomming;

		public async ValueTask PostAsync(byte priority, (TGenome Genome, Fitness[] Fitness) challenger)
		{
			Incomming[priority]
				.Enqueue(challenger);

			await OnAfterPost();
		}

		protected abstract ValueTask ProcessInjested(byte priority, (TGenome Genome, Fitness[] Fitness) challenger);

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

					await ProcessInjested(i, c);

					i = 0; // Reset to top queue.
					++count;

					goto retry;
				}
			}
			while (count != 0);
		}

		readonly double[][] BestProgressiveFitness;

		protected void ProcessChampion(int poolIndex, (TGenome Genome, Fitness[] Fitness) champ)
		{
			Debug.Assert(poolIndex >= 0);
			Tower.Problem.Pools[poolIndex].Champions?.Add(champ.Genome, champ.Fitness[poolIndex]);
			Tower.Broadcast(champ, poolIndex);
		}

		protected override async ValueTask<LevelEntry<TGenome>?> ProcessEntry((TGenome Genome, Fitness[] Fitness) champ)
		{
			var result = (await Tower.Problem.ProcessSampleAsync(champ.Genome, Index)).Select((fitness, i) =>
			{
				var values = fitness.Results.Sum;
				var progressiveFitness = champ.Fitness[i];
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

			if (result.Any(r => r.fresh) || !result.Any(r => r.success))
				return LevelEntry<TGenome>.Init(in champ, result.Select(r => r.values).ToImmutableArray());

			Factory.EnqueueChampion(champ.Genome);
			await PostNextLevelAsync(0, champ);
			return null;
		}

	}
}
