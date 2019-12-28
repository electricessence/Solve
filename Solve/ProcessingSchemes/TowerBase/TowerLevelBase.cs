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
			ReadOnlySpan<double> contending,
			int index)
		{
			Debug.Assert(contending != null);

			ref var fRef = ref registry[index];
			double[]? defending;
			double[]? contendingArray = null;
			while ((defending = fRef) == null || contending.IsGreaterThan(defending.AsSpan()))
			{
				contendingArray ??= contending.ToArray();
				if (Interlocked.CompareExchange(ref fRef!, contendingArray, defending) == defending)
					return (true, defending == null);
			}
			return (false, false);
		}

		protected abstract ValueTask<LevelEntry<TGenome>?> ProcessEntry((TGenome Genome, Fitness[] Fitness) champ);

		protected LevelEntry<TGenome>[][] RankEntries(LevelEntry<TGenome>[] pool)
			=> Tower.Problem.Pools
				.Select((p, i) => pool.OrderBy(e => e.Scores[i], CollectionComparer.Double.Descending).ToArray())
				.ToArray();
	}
}
