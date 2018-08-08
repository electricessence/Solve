using Open.Collections;
using Open.Memory;
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
			readonly ProblemTower Tower;
			public readonly ushort PoolSize;
			public readonly uint Index;
			Level _nextLevel;
			public Level NextLevel => LazyInitializer.EnsureInitialized(ref _nextLevel,
				() => new Level(Index + 1, Tower));

			private readonly BatchCreator<LevelEntry<TGenome>> Pool;

			public Level(
				uint level,
				ProblemTower tower)
			{
				Debug.Assert(level < tower.Environment.MaxLevels);

				Index = level;
				Tower = tower;
				var env = Tower.Environment;
				Factory = env.Factory[1]; // Use a lower priority than the factory used by broadcasting.

				var (First, Minimum, Step) = env.PoolSize;
				var maxDelta = First - Minimum;
				var decrement = Index * Step;

				var poolCount = tower.Problem.Pools.Count;
				BestLevelFitness = new double[poolCount][];
				BestProgressiveFitness = new double[poolCount][];
				//IsMaxLevel = level + 1 == tower.Environment.MaxLevels;

				PoolSize = decrement > maxDelta ? Minimum : (ushort)(First - decrement);
				Pool = new BatchCreator<LevelEntry<TGenome>>(PoolSize);
			}

			//public readonly bool IsMaxLevel;
			readonly IGenomeFactoryPriorityQueue<TGenome> Factory;

			readonly double[][] BestLevelFitness;
			readonly double[][] BestProgressiveFitness;

			static (bool success, bool isFresh) UpdateFitnessesIfBetter(
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


			(double[] Fitness,
				(bool Local, bool Progressive, bool Either) IsFresh,
				(bool Local, bool Progressive, bool Either) Superiority)[] ProcessTestAndUpdate(
				(TGenome Genome, Fitness[] Fitness) progress, IEnumerable<Fitness> fitnesses)
				=> fitnesses.Select((fitness, i) =>
				{
					var values = fitness.Results.Sum.ToArray();
					var (levSuccess, levIsFresh) = UpdateFitnessesIfBetter(BestLevelFitness, values, i);

					var progressiveFitness = progress.Fitness[i];
					var (proSuccess, proIsFresh) = UpdateFitnessesIfBetter(
						BestProgressiveFitness,
						progressiveFitness
							.Merge(values)
							.Average
							.ToArray(), i);

					Debug.Assert(progressiveFitness.MetricAverages.All(ma => ma.Value <= ma.Metric.MaxValue));

					return (values,
						(levIsFresh, proIsFresh, levIsFresh || proIsFresh),
						(levSuccess, proSuccess, levSuccess || proSuccess));
				}).ToArray();

			readonly ConcurrentQueue<Task> ExpressPool
				= new ConcurrentQueue<Task>();

			// ReSharper disable once UnusedMethodReturnValue.Local
			public Task PostExpress((TGenome Genome, Fitness[] Fitness) c, bool expressToTop = false)
			{
				var task = Tower.Problem
					.ProcessSampleAsync(c.Genome, Index)
					.ContinueWith(e => PostInternalFromQueue(c, e.Result, true, expressToTop));
				ExpressPool.Enqueue(task);
				return task;
			}

			readonly ConcurrentQueue<Task> StandbyPool
				= new ConcurrentQueue<Task>();

			// ReSharper disable once UnusedMethodReturnValue.Local
			public Task PostStandby((TGenome Genome, Fitness[] Fitness) c)
			{
				var task = Tower.Problem
					.ProcessSampleAsync(c.Genome, Index)
					.ContinueWith(e => PostInternalFromQueue(c, e.Result, false));
				StandbyPool.Enqueue(task);
				return task;
			}

			void PostInternalFromQueue((TGenome Genome, Fitness[] Fitness) c,
				IEnumerable<Fitness> fitnesses,
				bool express,
				bool expressToTop = false)
			{
				// For debugging...
				PostInternal(c, fitnesses, express, expressToTop);
			}

			void PostInternal(
				(TGenome Genome, Fitness[] Fitness) c,
				IEnumerable<Fitness> fitnesses,
				bool express,
				bool expressToTop = false)
			{
				// Process a test for this level.
				var result = ProcessTestAndUpdate(c, fitnesses);

				// If we are at a designated maximum, then the top is the top and anything else doesn't matter.
				// We've either become the top champion, or we are rejected.
				//if (IsMaxLevel)
				//	return;

				// Rules:
				//
				// Emit if:
				// - For a given level if it's new, then that means it's the first at that level.
				// - If it's no

				bool postIfSuperior()
				{
					if (!result.Any(f => f.Superiority.Local || express && f.Superiority.Progressive)) return false;
					Factory.EnqueueChampion(c.Genome);
					// Local or progressive winners should be accellerated.
					NextLevel.Post(c, true);
					return true; // No need to involve a obviously superior genome with this pool.
				}

				if (_nextLevel != null)
				{
					if (expressToTop)
					{
						// Local or progressive winners should be accellerated.
						_nextLevel.Post(c, true, true); // expressToTop... Since this is the reigning champ for this pool (or expressToTop).
						return; // No need to involve a obviously superior genome with this pool.
					}

					if (postIfSuperior())
					{
						return;
					}
				}

				var challenger = new LevelEntry<TGenome>(c, result.Select(f => f.Fitness).ToArray());
				Pool.Add(challenger);
			}

			public void Post((TGenome Genome, Fitness[] Fitness) c,
				bool express = false,
				bool expressToTop = false,
				bool processPool = false)
			{
				PostInternal(c,
					Tower.Problem.ProcessSample(c.Genome, Index),
					express, expressToTop);

				if (processPool)
					ProcessPoolAsync().Wait();
			}

			public async Task PostAsync((TGenome Genome, Fitness[] Fitness) c,
				CancellationToken token,
				bool express = false,
				bool expressToTop = false,
				bool processPool = false)
			{
				PostInternal(c,
					await Tower.Problem.ProcessSampleAsync(c.Genome, Index),
					express, expressToTop);

				if (processPool)
					await ProcessPoolAsync(token);
			}

			bool ProcessPoolInternal()
			{
				if (Pool.TryDequeue(out var pool))
					return false;

				// 1) Setup selection.
				var len = pool.Length;
				var midPoint = pool.Length / 2;

				var promoted = new HashSet<string>();

				var problemPools = Tower.Problem.Pools;
				var problemPoolCount = problemPools.Count;
				var selection = new LevelEntry<TGenome>[problemPoolCount][];
				var isTop = _nextLevel == null;
				for (var i = 0; i < problemPoolCount; i++)
				{
					var p = problemPools[i];
					var s = pool
						.OrderBy(e => e.Scores[i], ArrayComparer<double>.Descending)
						.ToArray();
					selection[i] = s;

					// 2) Signal & promote champions.
					var champ = s[0].GenomeFitness;
					p.Champions?.Add(champ.Genome, champ.Fitness[i]);
					if (promoted.Add(champ.Genome.Hash))
					{
						Factory.EnqueueChampion(champ.Genome);
						NextLevel.Post(champ, true); // Champs may need to be posted synchronously to stay ahead of other deferred winners.
					}

					if (isTop) Tower.Broadcast(champ, i);

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
							NextLevel.PostExpress(winner); // PostStandby?
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

							//#if DEBUG
							//							if (IsTrackedGenome(gf.Genome.Hash))
							//								Debugger.Break();
							//#endif

						}
					}
					else
					{
						Debug.Assert(loser.LevelLossRecord > 0);
						Pool.Add(loser); // Didn't win, but still in the game.
					}
				}
				return true;
			}

			public async Task ProcessPoolAsync(CancellationToken token = default, bool thisLevelOnly = false)
			{
				retry:

				var processed = false;
				if (!ExpressPool.IsEmpty)
				{
					if (token.IsCancellationRequested) return;
					await Task.WhenAll(ExpressPool.AsDequeueingEnumerable());
					if (token.IsCancellationRequested) return;
					processed = ProcessPoolInternal();
				}

				if (!StandbyPool.IsEmpty)
				{
					if (token.IsCancellationRequested) return;
					await Task.WhenAll(StandbyPool.AsDequeueingEnumerable());
				}

				if (token.IsCancellationRequested) return;
				processed = ProcessPoolInternal() || processed;

				if (!thisLevelOnly)
				{
					// walk up instead of recurse.
					var next = this;
					while (!token.IsCancellationRequested && (next = next._nextLevel) != null)
						await next.ProcessPoolAsync(token, true);
				}

				if (!token.IsCancellationRequested && processed)
					goto retry;
			}

		}

	}
}
