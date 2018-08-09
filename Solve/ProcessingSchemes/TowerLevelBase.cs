using Open.Memory;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	public abstract class TowerLevelBase<TGenome, TTower>
		where TGenome : class, IGenome
		where TTower : ProblemTowerBase<TGenome>
	{
		protected readonly uint Index;
		protected readonly ushort PoolSize;
		protected readonly TTower Tower;
		protected readonly IGenomeFactoryPriorityQueue<TGenome> Factory;

		protected TowerLevelBase(
			uint level,
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
			Factory.EnqueueChampion(champ.Genome);
			Tower.Problem.Pools[poolIndex].Champions?.Add(champ.Genome, champ.Fitness[poolIndex]);
			Tower.Broadcast(champ, poolIndex);
		}

		protected async Task<LevelEntry<TGenome>> ProcessEntry((TGenome Genome, Fitness[] Fitness) c)
		{
			var result = (await Tower.Problem.ProcessSampleAsync(c.Genome, Index)).Select((fitness, i) =>
			{
				var values = fitness.Results.Sum.ToArray();
				var progressiveFitness = c.Fitness[i];
				var (success, fresh) = UpdateFitnessesIfBetter(
					BestProgressiveFitness,
					progressiveFitness
						.Merge(values)
						.Average
						.ToArray(), i);

				Debug.Assert(progressiveFitness.MetricAverages.All(ma => ma.Value <= ma.Metric.MaxValue));

				return (values, success, fresh);
			}).ToArray();

			if (result.Any(r => r.fresh) || !result.Any(r => r.success))
				return new LevelEntry<TGenome>(in c, result.Select(r => r.values).ToArray());

			Factory.EnqueueChampion(c.Genome);
			PostNextLevel(0, c);
			return null;
		}

	}
}
