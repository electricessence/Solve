using Open.Memory;
using System;
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
		public readonly int Index;
		protected readonly TTower Tower;

		protected TowerLevelBase(
			int level,
			TTower tower)
		{
			Debug.Assert(level >= 0);

			Index = level;
			Tower = tower;
		}

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

		protected abstract ValueTask<LevelEntry<TGenome>> ProcessEntry((TGenome Genome, Fitness[] Fitness) champ);

		protected LevelEntry<TGenome>[][] RankEntries(LevelEntry<TGenome>[] pool)
			=> Tower.Problem.Pools
				.Select((p, i) => pool.OrderBy(e => e.Scores[i], ArrayComparer<double>.Descending).ToArray())
				.ToArray();
	}
}
