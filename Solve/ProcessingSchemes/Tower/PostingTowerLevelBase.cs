using Open.Collections;
using Open.Memory;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes.Tower
{
	public abstract class PostingTowerLevelBase<TGenome, TTower, TEnvironment> : TowerLevelBase<TGenome, TTower, TEnvironment>
		where TGenome : class, IGenome
		where TTower : TowerBase<TGenome, TEnvironment>
		where TEnvironment : EnvironmentBase<TGenome>
	{
		protected PostingTowerLevelBase(
			int level,
			TTower tower,
			byte priorityLevels) :base(level, tower)
		{
			var poolCount = tower.Problem.Pools.Count;
			BestLevelFitness = new double[poolCount][];
			BestProgressiveFitness = new double[poolCount][];

			Factory = tower.Environment.Factory[1]; // Use a lower priority than the factory used by broadcasting.

			Challengers = Enumerable
				.Range(0, priorityLevels)
				.Select(i => new ConcurrentQueue<(TGenome Genome, Fitness[] Fitness)>())
				.ToArray();
		}

		protected readonly IGenomeFactoryPriorityQueue<TGenome> Factory;

		readonly ConcurrentQueue<(TGenome Genome, Fitness[] Fitness)>[] Challengers;

		readonly ConcurrentQueue<LevelEntry<TGenome>> Retained
			= new ConcurrentQueue<LevelEntry<TGenome>>();

		protected abstract PostingTowerLevelBase<TGenome, TTower, TEnvironment> NextLevel { get; }
		protected abstract bool IsTop { get; }

		readonly double[][] BestLevelFitness;
		readonly double[][] BestProgressiveFitness;

		protected static (bool success, bool isFresh) UpdateFitnessesIfBetter(
			ref double[] defending,
			double[] contending)
		{
			Debug.Assert(contending != null);

			double[] d;
			while ((d = defending) == null || contending.IsGreaterThan(d))
			{
				if (Interlocked.CompareExchange(ref defending, contending, d) == d)
					return (true, d == null);
			}
			return (false, false);
		}

		protected static (bool success, bool isFresh) UpdateFitnessesIfBetter(
			Span<double[]> registry, int index,
			double[] contending)
			=> UpdateFitnessesIfBetter(ref registry[index], contending);

		protected LevelEntry<TGenome>[][] RankEntries(LevelEntry<TGenome>[] pool)
			=> Tower.Problem.Pools
				.Select((p, i) => pool.OrderBy(e => e.Scores[i], ArrayComparer<double>.Descending).ToArray())
				.ToArray();

		public bool TryGetChallenger(out (TGenome Genome, Fitness[] Fitness) challenger)
		{
			foreach (var queue in Challengers)
			{
				if (queue.TryDequeue(out challenger))
					return true;
			}

			challenger = default;
			return false;
		}

		public ValueTask<bool> ProcessChallenger()
			=> TryGetChallenger(out var challenger)
			? ProcessChallengerCore(challenger)
			: new ValueTask<bool>(false);

		async ValueTask<bool> ProcessChallengerCore((TGenome Genome, Fitness[] Fitness) challenger)
		{
			var result = (await Tower.Problem.ProcessSampleAsync(challenger.Genome, Index)).Select((fitness, i) =>
			{
				var values = fitness.Results.Sum.ToArray();

				var (successLocal, freshLocal)
					= UpdateFitnessesIfBetter(BestLevelFitness, i, values);

				var progressiveFitness = challenger.Fitness[i];
				var (successProgressive, freshProgressive)
					= UpdateFitnessesIfBetter(BestProgressiveFitness, i,
						progressiveFitness //progressive fitness
							.Merge(values)
							.Average
							.ToArray());

				//Debug.Assert(progressiveFitness.MetricAverages.All(ma => ma.Value <= ma.Metric.MaxValue));

				return (
					values,
					success: successLocal || successProgressive,
					fresh: freshLocal || freshProgressive);

			}).ToArray();

			if (IsTop)
			{
				for (byte i = 0; i < result.Length; i++)
				{
					if (result[i].success)
						SignalChampion(i, challenger);
				}
			}

			if (result.Any(r => r.fresh) || !result.All(r => r.success))
			{
				Retain(in challenger, result.Select(r => r.values).ToArray());
			}
			else
			{
				Factory.EnqueueChampion(challenger.Genome);
				Promote(0, challenger);
			}

			return true;
		}

		protected virtual void Retain(in (TGenome Genome, Fitness[] Fitness) contender, double[][] values)
		{
			Retained.Enqueue(LevelEntry<TGenome>.Init(in contender, values));
		}

		public ValueTask ProcessRetained()
			=> ProcessRetained(Retained);

		protected abstract ValueTask ProcessRetained(ConcurrentQueue<LevelEntry<TGenome>> retained);

		protected virtual void SignalChampion(byte poolIndex, (TGenome Genome, Fitness[] Fitness) champ)
		{
			//Tower.Problem.Pools[poolIndex].Champions?.Add(champ.Genome, champ.Fitness[poolIndex]);
			Tower.Broadcast(champ, poolIndex);
		}

		public virtual void Post(byte priority, (TGenome Genome, Fitness[] Fitness) challenger)
		{
			Challengers[priority].Enqueue(challenger);
		}

		protected void Promote(byte priority, (TGenome Genome, Fitness[] Fitness) challenger)
			=> NextLevel.Post(priority, challenger);
	}
}
