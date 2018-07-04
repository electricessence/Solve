using Open.Collections;
using Open.Memory;
using Open.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Solve.ProcessingSchemes
{
	public sealed partial class TowerProcessingScheme<TGenome>
	{
		sealed class Level
		{
			class Entry
			{
				public Entry((TGenome Genome, Fitness[] Fitness) gf, double[][] scores)
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

			bool IsTop => _nextLevel == null;

			readonly ConcurrentQueue<Entry> Pool
				= new ConcurrentQueue<Entry>();

			public Level(
				uint level,
				ProblemTower tower)
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
			}

			readonly IGenomeFactoryPriorityQueue<TGenome> Factory;

			readonly double[][] BestLevelFitness;
			readonly double[][] BestProgressiveFitness;

			static bool UpdateFitnessesIfBetter(
				Span<double[]> registry,
				double[] contending,
				int index)
			{
				Debug.Assert(contending != null);

				ref double[] fRef = ref registry[index];
				double[] defending;
				while ((defending = fRef) == null || contending.IsGreaterThan(defending))
				{
					if (Interlocked.CompareExchange(ref fRef, contending, defending) == defending)
						return true;
				}
				return false;
			}


			(double[] Fitness, (bool Local, bool Progressive, bool Either) Superiority)[] ProcessTestAndUpdate(
				(TGenome Genome, Fitness[] Fitness) c)
				=> Tower.Problem.ProcessSample(c.Genome, Index).Select((fitness, i) =>
				{
					var values = fitness.Results.Sum.ToArray();
					var lev = UpdateFitnessesIfBetter(BestLevelFitness, values, i);

					var progressiveFitness = c.Fitness[i];
					var pro = UpdateFitnessesIfBetter(BestProgressiveFitness, progressiveFitness.Merge(values).Average.ToArray(), i);

					Debug.Assert(progressiveFitness.MetricAverages.All(ma => ma.Value <= ma.Metric.MaxValue));

					return (values, (lev, pro, lev || pro));
				}).ToArray();

			public void Post(
				(TGenome Genome, Fitness[] Fitness) c,
				bool express = false,
				bool expressToTop = false)
			{
				// Process a test for this level.
				var result = ProcessTestAndUpdate(c);
				Debug.Assert(c.Fitness.All(f => f.Results != null));
				if (_nextLevel != null)
				{
					bool either;
					if (_nextLevel.IsTop && (either = result.Any(f => f.Superiority.Either)))
					{
						Factory.EnqueueChampion(c.Genome);
					}
					else
					{
						either = true;
					}

					if (expressToTop || either && result.Any(f => f.Superiority.Local || express && f.Superiority.Progressive))
					{
						_nextLevel.Post(c, express, true); // expresssToTop... Since this is the reigning champ for this pool (or expressToTop).
						return; // No need to involve a obviously superior genome with this pool.
					}
				}

				var challenger = new Entry(c, result.Select(f => f.Fitness).ToArray());
				Pool.Enqueue(challenger);

				// Next see if we should 'own' processing the pool.
				if (Pool.Count >= PoolSize)
				{
					Entry[] pool = null;
					// If a lock is already aquired somewhere else, then skip/ignore...
					if (ThreadSafety.TryLock(Pool, () =>
					{
						if (Pool.Count >= PoolSize)
							pool = Pool.AsDequeueingEnumerable().Take(PoolSize).ToArray();
					}) && pool != null)
					{
						// 1) Setup selection.
						var len = pool.Length;
						var midPoint = pool.Length / 2;
						var lastLevel = _nextLevel == null;

						var selections = Tower.Problem
							.Pools
							.Select((f, i) =>
							{
								var s = pool
									.OrderBy(e => e.Scores[i], ArrayComparer<double>.Descending)
									.ToArray();

								var champions = f.Champions;
								if (champions != null)
								{
									var t = s[0].GenomeFitness;
									champions.Add(t.Genome, t.Fitness[i]); // Need to leverage potentially significant genetics...
								}

								var toReject = s.AsSpan().Slice(midPoint);
								var rem = toReject.Length;
								// 2) Increment fitness rejection for individual fitnesses.
								for (var n = 0; n < rem; n++)
									toReject[n].GenomeFitness.Fitness[i].IncrementRejection();

								return s;
							})
							.ToArray();

						// 3) Weave all fitnesses to sort out winners and losers.
						var ordered = selections
							.Weave()
							.Distinct()
							.ToArray()
							.AsSpan();

						Debug.Assert(pool.Length == ordered.Length);

						var winners = ordered.Slice(0, midPoint);
						var losers = ordered.Slice(midPoint);

						// 4) Return losers to pool.
						var losersToPromote = new List<Entry>();
						var maxLoses = Tower.Environment.MaxLevelLosses;
						var maxRejection = Tower.Environment.MaxLossesBeforeElimination;
						foreach (var loser in losers)
						{
							loser.LevelLossRecord++;

							if (loser.LevelLossRecord > maxLoses)
							{
								var fitness = loser.GenomeFitness.Fitness;
								if (fitness.Any(f => f.RejectionCount < maxRejection))
									losersToPromote.Add(loser);
								else
								{
									//Host.Problem.Reject(loser.GenomeFitness.Genome.Hash);
									((GenomeFactoryBase<TGenome>)Tower.Environment.Factory).MetricsCounter.Increment("Genome Rejected");
								}
							}
							else
							{
								Debug.Assert(loser.LevelLossRecord > 0);
								Pool.Enqueue(loser);
							}
						}


						// 5) Promote winners.
						var top = winners[0].GenomeFitness;
						NextLevel.Post(top, true); // express: give it the opportunity to keep going.
						for (var i = 1; i < midPoint; i++)
							NextLevel.Post(winners[i].GenomeFitness);

						// 6) Add top winners for each problem to their respective ranked pools.


						// 7) Promote second chance losers
						foreach (var loser in losersToPromote)
						{
							NextLevel.Post(loser.GenomeFitness);
						}

						// Broadcast level winner.
						if (lastLevel)
						{
							Tower.Broadcast(top);
							//((GenomeFactoryBase<TGenome>)Tower.Environment.Factory).MetricsCounter.Increment("Top Level Pool Selected");
						}

					}
				}
			}
		}

	}
}
