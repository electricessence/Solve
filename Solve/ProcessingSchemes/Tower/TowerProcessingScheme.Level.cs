﻿using Open.Collections;
using Open.Memory;
using Open.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	// ReSharper disable once PossibleInfiniteInheritance
	[SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
	public sealed partial class TowerProcessingScheme<TGenome>
	{
		sealed class Level
		{
			class Entry
			{
				public Entry(in (TGenome Genome, Fitness[] Fitness) gf, double[][] scores)
				{
					GenomeFitness = gf;
					Scores = scores;
					LevelLossRecord = 0;
				}

				public readonly (TGenome Genome, Fitness[] Fitness) GenomeFitness;
				public readonly double[][] Scores;

				public ushort LevelLossRecord;
			}

			readonly ProblemTower Tower;
			public readonly ushort PoolSize;
			public readonly uint Index;
			Level _nextLevel;
			public Level NextLevel => LazyInitializer.EnsureInitialized(ref _nextLevel,
				() => new Level(Index + 1, Tower));

			bool IsCurrentTop => _nextLevel == null;

			readonly ConcurrentQueue<Entry> Pool
				= new ConcurrentQueue<Entry>();

			public Level(
				uint level,
				in ProblemTower tower)
			{
				Index = level;
				Tower = tower;
				var env = Tower.Environment;
				Factory = env.Factory[1]; // Use a lower priority than the factory used by broadcasting.

				var (First, Minimum, Step) = env.PoolSize;
				var maxDelta = First - Minimum;
				var decrement = Index * Step;
				PoolSize = decrement > maxDelta ? Minimum : (ushort)(First - decrement);

				BestLevelFitness = new double[tower.Problem.Pools.Count][];
				BestProgressiveFitness = new double[tower.Problem.Pools.Count][];
				Debug.Assert(level < tower.Environment.MaxLevels);
				IsMaxLevel = level + 1 == tower.Environment.MaxLevels;
			}

			public readonly bool IsMaxLevel;
			readonly IGenomeFactoryPriorityQueue<TGenome> Factory;

			readonly double[][] BestLevelFitness;
			readonly double[][] BestProgressiveFitness;

			static bool UpdateFitnessesIfBetter(
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
						return true;
				}
				return false;
			}


			(double[] Fitness, (bool Local, bool Progressive, bool Either) Superiority)[] ProcessTestAndUpdate(
				(TGenome Genome, Fitness[] Fitness) progress, IEnumerable<Fitness> fitnesses)
				=> fitnesses.Select((fitness, i) =>
				{
					var values = fitness.Results.Sum.ToArray();
					var lev = UpdateFitnessesIfBetter(BestLevelFitness, values, i);

					var progressiveFitness = progress.Fitness[i];
					var pro = UpdateFitnessesIfBetter(
						BestProgressiveFitness,
						progressiveFitness
							.Merge(values)
							.Average
							.ToArray(), i);

					Debug.Assert(progressiveFitness.MetricAverages.All(ma => ma.Value <= ma.Metric.MaxValue));

					return (values, (lev, pro, lev || pro));
				}).ToArray();

			void PromoteChampion(TGenome genome)
			{
				Factory.EnqueueChampion(genome);
				Factory.EnqueueForMutation(genome);
				//Factory.EnqueueForMutation(genome);
				//Factory.EnqueueForBreeding(genome);
				//Factory.EnqueueForBreeding(genome);
			}

			readonly ConcurrentQueue<Task> ExpressPool
				= new ConcurrentQueue<Task>();

			public void PostExpress((TGenome Genome, Fitness[] Fitness) c)
				=> ExpressPool.Enqueue(
					Tower.Problem
						.ProcessSampleAsync(c.Genome, Index)
						.ContinueWith(e => PostInternalFromQueue(c, e.Result, true)));

			readonly ConcurrentQueue<Task> StandbyPool
				= new ConcurrentQueue<Task>();

			public void PostStandby((TGenome Genome, Fitness[] Fitness) c)
				=> StandbyPool.Enqueue(
					Tower.Problem
						.ProcessSampleAsync(c.Genome, Index)
						.ContinueWith(e => PostInternalFromQueue(c, e.Result, false)));

			void PostInternalFromQueue((TGenome Genome, Fitness[] Fitness) c,
				IEnumerable<Fitness> fitnesses,
				bool express)
			{
				// For debugging...
				PostInternal(c, fitnesses, express);
			}

			void PostInternal(
				(TGenome Genome, Fitness[] Fitness) c,
				IEnumerable<Fitness> fitnesses,
				bool express,
				bool expressToTop = false)
			{
				// Process a test for this level.
				var result = ProcessTestAndUpdate(c, fitnesses);
				Debug.Assert(c.Fitness.All(f => f.Results != null));

				// If we are at a designated maximum, then the top is the top and anything else doesn't matter.
				if (IsMaxLevel)
				{
					if (!result.Any(f => f.Superiority.Progressive)) return;
					PromoteChampion(c.Genome);
					Tower.Broadcast(c);
					return; // If we've reached the top, we've either become the top champion, or we are rejected.
				}

				// If we aren't the top, or our pool is full, go ahead and check if we should promote early.
				// Example: if the incomming genome is already identified as superior, then no need to enter this pool.
				if (_nextLevel != null || Pool.Count >= PoolSize)
				{
					bool either;
					if (NextLevel.IsCurrentTop)
					{
						either = result.Any(f => f.Superiority.Either);
						if (either) PromoteChampion(c.Genome);
					}
					else
					{
						either = true;
					}

					if (expressToTop || either && result.Any(f => f.Superiority.Local || express && f.Superiority.Progressive))
					{
						NextLevel.Post(c, express, true); // expresssToTop... Since this is the reigning champ for this pool (or expressToTop).
						return; // No need to involve a obviously superior genome with this pool.
					}
				}

				var challenger = new Entry(c, result.Select(f => f.Fitness).ToArray());
				Pool.Enqueue(challenger);
			}

			public void Post((TGenome Genome, Fitness[] Fitness) c,
				bool express = false,
				bool expressToTop = false,
				bool processPool = false)
			{
				PostInternal(c, Tower.Problem.ProcessSample(c.Genome, Index), express, expressToTop);

				if (processPool) ProcessPoolAsync().Wait();
			}

			public async Task PostAsync((TGenome Genome, Fitness[] Fitness) c,
				bool express = false,
				bool expressToTop = false,
				bool processPool = false)
			{
				PostInternal(c, await Tower.Problem.ProcessSampleAsync(c.Genome, Index), express, expressToTop);

				if (processPool) await ProcessPoolAsync();
			}

			bool ProcessPoolInternal()
			{
				if (Pool.Count < PoolSize) return false;

				Entry[] pool = null;
				// If a lock is already aquired somewhere else, then skip/ignore...
				if (!ThreadSafety.TryLock(Pool, () =>
				{
					if (Pool.Count >= PoolSize)
						pool = Pool.AsDequeueingEnumerable().Take(PoolSize).ToArray();
				}) || pool == null) return true;

				// 1) Setup selection.
				var len = pool.Length;
				var midPoint = pool.Length / 2;
				var lastLevel = _nextLevel == null;

				var promoted = new HashSet<string>();

				var problemPools = Tower.Problem.Pools;
				var problemPoolCount = problemPools.Count;
				var selection = new Entry[problemPoolCount][];
				for (var i = 0; i < problemPoolCount; i++)
				{
					var p = problemPools[i];
					var s = pool
						.OrderBy(e => e.Scores[i], ArrayComparer<double>.Descending)
						.ToArray();
					selection[i] = s;

					// 2) Signal & promote champions.
					var champ = s[0].GenomeFitness;
					if (promoted.Add(champ.Genome.Hash))
					{
						p.Champions?.Add(champ.Genome, champ.Fitness[i]); // Need to leverage potentially significant genetics...
						if (lastLevel) Tower.Broadcast(champ);
						NextLevel.PostExpress(champ);
					}

					// 3) Increment fitness rejection for individual fitnesses.
					for (var n = midPoint; n < len; n++)
					{
						s[n].GenomeFitness.Fitness[i].IncrementRejection();
					}
				}

				// 4) Promote remaining winners (weaving to ensure that other threads honor the priority)
				for (var n = 1; n < midPoint; n++)
				{
					for (var i = 0; i < problemPoolCount; i++)
					{
						var s = selection[i];
						var winner = s[n].GenomeFitness;
						if (promoted.Add(winner.Genome.Hash))
							NextLevel.PostExpress(winner);
					}
				}

				//NextLevel.ProcessPool(true); // Prioritize winners and express.

				// 5) Process remaining (losers)
				var maxLoses = Tower.Environment.MaxLevelLosses;
				var maxRejection = Tower.Environment.MaxLossesBeforeElimination;
				foreach (var loser in selection.Select(s => s.Skip(midPoint)).Weave().Distinct())
				{
					if (promoted.Contains(loser.GenomeFitness.Genome.Hash)) continue;
					loser.LevelLossRecord++;

					if (loser.LevelLossRecord > maxLoses)
					{
						var gf = loser.GenomeFitness;
						var fitnesses = gf.Fitness;
						if (fitnesses.Any(f => f.RejectionCount < maxRejection))
							NextLevel.PostStandby(gf);
						else
						{
							//Host.Problem.Reject(loser.GenomeFitness.Genome.Hash);
							if (Tower.Environment.Factory is GenomeFactoryBase<TGenome> f)
								f.MetricsCounter.Increment("Genome Rejected");
						}
					}
					else
					{
						Debug.Assert(loser.LevelLossRecord > 0);
						Pool.Enqueue(loser); // Didn't win, but still in the game.
					}
				}
				return true;
			}

			public async Task ProcessPoolAsync(bool thisLevelOnly = false)
			{
				retry:
				var processed = false;
				if (!ExpressPool.IsEmpty)
				{
					await Task.WhenAll(ExpressPool.AsDequeueingEnumerable());
					processed = ProcessPoolInternal();
				}

				if (!StandbyPool.IsEmpty)
				{
					await Task.WhenAll(StandbyPool.AsDequeueingEnumerable());
				}

				processed = ProcessPoolInternal() || processed;

				if (!thisLevelOnly)
				{
					// walk up instead of recurse.
					var next = this;
					while ((next = next._nextLevel) != null)
						await next.ProcessPoolAsync(true);
				}
				if (processed) goto retry;
			}

		}

	}
}
