﻿using Open.Memory;
using System;
using System.Buffers;
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
			in SchemeConfig.PoolSizing poolSize,
			TTower tower)
			: this(level, poolSize.GetPoolSize(level), tower)
		{
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

		protected abstract ValueTask<LevelEntry<TGenome>?> ProcessEntry(LevelProgress<TGenome> champ);

		protected LevelEntry<TGenome>[][] RankEntries(IList<LevelEntry<TGenome>> pool)
		{
			var len = pool.Count;
			var poolCount = Tower.Problem.Pools.Count;
			var result = ArrayPool<LevelEntry<TGenome>[]>.Shared.Rent(poolCount);

			for (var i = 0; i < poolCount; i++)
			{
				var temp = new LevelEntry<TGenome>[len];
				result[i] = temp;

				pool.CopyTo(temp, 0);
				var comparer = LevelEntry<TGenome>.GetScoreComparer(i);
#if DEBUG
				Debug.Assert(temp.Take(len).All(e => e != null));

				try
				{
					Array.Sort(temp, 0, len, comparer);
				}
				catch (Exception ex)
				{
					// Find the problem.
					var trouble = new List<LevelEntry<TGenome>>();
					var suspect = new List<LevelEntry<TGenome>>();
					FindTroubledEntries(0, len);
					Debug.WriteLine("Found {0} unsortalbe entries.", trouble.Count);
					Debug.WriteLine(ex.ToString());
					Debugger.Break();
					throw;

					void FindTroubledEntries(int start, int end)
					{
						var distance = end - start;
						if (distance == 1) trouble.Add(temp[start]);
						if (distance < 2) return;
						var half = start + distance / 2;
						var count = trouble.Count;
						try
						{
							Array.Sort(temp, start, half, comparer);
						}
						catch
						{
							FindTroubledEntries(start, half);
						}
						try
						{
							Array.Sort(temp, half, end, comparer);
						}
						catch
						{
							FindTroubledEntries(half, end);
						}

						// None added, but clearly a culprit?
						if (distance == 2 && count == trouble.Count)
						{
							suspect.Add(temp[start]);
							suspect.Add(temp[start + 1]);
						}
					}
				}
#else
				Array.Sort(temp, 0, len, comparer);
#endif

				try
				{
					Array.Sort(temp, 0, len, comparer);
				}
				catch (Exception ex)
				{
					// Find the problem.
					var trouble = new List<LevelEntry<TGenome>>();
					var suspect = new List<LevelEntry<TGenome>>();
					FindTroubledEntries(0, len);
					Debug.WriteLine("Found {0} unsortalbe entries.", trouble.Count);
					Debug.WriteLine(ex.ToString());
					Debugger.Break();
					throw;

					void FindTroubledEntries(int start, int end)
					{
						var distance = end - start;
						if (distance == 1) trouble.Add(temp[start]);
						if (distance < 2) return;
						var half = start + distance / 2;
						var count = trouble.Count;
						try
						{
							Array.Sort(temp, start, half, comparer);
						}
						catch
						{
							FindTroubledEntries(start, half);
						}
						try
						{
							Array.Sort(temp, half, end, comparer);
						}
						catch
						{
							FindTroubledEntries(half, end);
						}

						// None added, but clearly a culprit?
						if(distance==2 && count== trouble.Count)
						{
							suspect.Add(temp[start]);
							suspect.Add(temp[start+1]);
						}
					}
				}
			}

			return result;
		}
	}
}
