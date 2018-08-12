using Open.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	public abstract class TowerLevelBase<TGenome, TTower>
		where TGenome : class, IGenome
		where TTower : TowerBase<TGenome>
	{
		protected readonly int Index;
		protected readonly ushort PoolSize;
		protected readonly TTower Tower;
		protected readonly IGenomeFactoryPriorityQueue<TGenome> Factory;
		protected abstract bool IsTop { get; }

		protected TowerLevelBase(
			int level,
			TTower tower,
			byte priorityLevels)
		{
			var env = tower.Environment;
			Debug.Assert(level < env.MaxLevels);

			Index = level;
			Tower = tower;

			Factory = env.Factory[1]; // Use a lower priority than the factory used by broadcasting.

			var (First, Minimum, Step) = env.PoolSize;
			var maxDelta = First - Minimum;
			var decrement = Index * Step;
			PoolSize = decrement > maxDelta ? Minimum : (ushort)(First - decrement);

			BestProgressiveFitness = new double[tower.Problem.Pools.Count][];

			Incomming = Enumerable
				.Range(0, priorityLevels)
				.Select(i => new ConcurrentQueue<(TGenome Genome, Fitness[] Fitness)>())
				.ToArray();
		}

		protected abstract void PostNextLevel(byte priority, (TGenome Genome, Fitness[] Fitness) challenger);

		protected readonly ConcurrentQueue<(TGenome Genome, Fitness[] Fitness)>[] Incomming;

		public void Post(byte priority, (TGenome Genome, Fitness[] Fitness) challenger)
		{
			Incomming[priority]
				.Enqueue(challenger);

			OnAfterPost();
		}

		protected abstract void ProcessInjested(byte priority, (TGenome Genome, Fitness[] Fitness) challenger);

		protected virtual void OnAfterPost()
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

					ProcessInjested(i, c);

					i = 0; // Reset to top queue.
					++count;

					goto retry;
				}
			}
			while (count != 0);
		}

		protected static (bool success, bool isFresh) UpdateFitnessesIfBetter(
			Span<double[]> registry,
			double[] contending,
			int index)
		{
			Debug.Assert(contending != null);

			ref var fRef = ref registry[index];
			double[] defending;
			while ((defending = fRef) == null || contending.IsGreaterThan(defending))
			{
				if (Interlocked.CompareExchange(ref fRef, contending, defending) == defending)
					return (true, defending == null);
			}
			return (false, false);
		}

		readonly double[][] BestProgressiveFitness;

		protected void ProcessChampion(byte poolIndex, (TGenome Genome, Fitness[] Fitness) champ)
		{
			Tower.Problem.Pools[poolIndex].Champions?.Add(champ.Genome, champ.Fitness[poolIndex]);
			Tower.Broadcast(champ, poolIndex);
		}

		protected async Task<LevelEntry> ProcessEntry((TGenome Genome, Fitness[] Fitness) champ)
		{
			var result = (await Tower.Problem.ProcessSampleAsync(champ.Genome, Index)).Select((fitness, i) =>
			{
				var values = fitness.Results.Sum.ToArray();
				var progressiveFitness = champ.Fitness[i];
				var (success, fresh) = UpdateFitnessesIfBetter(
					BestProgressiveFitness,
					progressiveFitness
						.Merge(values)
						.Average
						.ToArray(), i);

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
				return new LevelEntry(in champ, result.Select(r => r.values).ToArray());

			Factory.EnqueueChampion(champ.Genome);
			PostNextLevel(0, champ);
			return null;
		}

		protected LevelEntry[][] RankEntries(LevelEntry[] pool)
			=> Tower.Problem.Pools
				.Select((p, i) => pool.OrderBy(e => e.Scores[i], ArrayComparer<double>.Descending).ToArray())
				.ToArray();

		protected class LevelEntry
		{
			public LevelEntry(in (TGenome Genome, Fitness[] Fitness) gf, double[][] scores)
			{
				GenomeFitness = gf;
				Scores = scores;
				LevelLossRecord = 0;
			}

			public readonly (TGenome Genome, Fitness[] Fitness) GenomeFitness;
			public readonly double[][] Scores;

			public ushort LevelLossRecord;

			public static LevelEntry Merge(in (TGenome Genome, Fitness[] Fitness) gf, IReadOnlyList<Fitness> scores)
			{
				var progressive = gf.Fitness;
				var len = scores.Count;
				var dScore = new double[scores.Count][];
				Debug.Assert(len == progressive.Length);

				for (var i = 0; i < len; i++)
				{
					var score = scores[i].Results.Sum.ToArray();
					dScore[i] = score;
					progressive[i].Merge(score);
				}

				return new LevelEntry(in gf, dScore);
			}
		}
	}
}
