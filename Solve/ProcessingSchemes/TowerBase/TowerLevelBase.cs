using Open.Memory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	public abstract class TowerLevelBase<TGenome, TTower, TEnvironment>
		where TGenome : class, IGenome
		where TTower : TowerBase<TGenome, TEnvironment>
		where TEnvironment : EnvironmentBase<TGenome>
	{
		protected readonly int Index;
		protected readonly ushort PoolSize;
		protected readonly TTower Tower;

		protected TowerLevelBase(
			int level,
			ushort poolSize,
			TTower tower)
		{
			Debug.Assert(level >= 0);

			Index = level;
			PoolSize = poolSize;
			Tower = tower;
		}

		protected TowerLevelBase(
			int level,
			in (ushort First, ushort Minimum, ushort Step) poolSize,
			TTower tower)
			: this(level, GetPoolSize(level, poolSize), tower)
		{
		}

		internal static ushort GetPoolSize(int level, in (ushort First, ushort Minimum, ushort Step) poolSize)
		{
			var (First, Minimum, Step) = poolSize;
			var maxDelta = First - Minimum;
			var decrement = level * Step;
			return decrement > maxDelta ? Minimum : (ushort)(First - decrement);
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

		protected abstract Task<LevelEntry> ProcessEntry((TGenome Genome, Fitness[] Fitness) champ);

		protected LevelEntry[][] RankEntries(LevelEntry[] pool)
			=> Tower.Problem.Pools
				.Select((p, i) => pool.OrderBy(e => e.Scores[i], ArrayComparer<double>.Descending).ToArray())
				.ToArray();

		protected class LevelEntry
		{
			public LevelEntry(in (TGenome Genome, Fitness[] Fitness) gf, double[][] scores, ushort levelLossRecord = 0)
			{
				GenomeFitness = gf;
				Scores = scores;
				LevelLossRecord = levelLossRecord;
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
