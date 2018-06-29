using Open.Collections;
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
		protected override void Post(TGenome genome)
		{
			Root.Post((genome, Enumerable.Range(0, Problems.Count).Select(i => new Fitness()).ToArray()));
		}

		internal sealed class Level
		{
			readonly TowerProcessingScheme<TGenome> Tower;
			public readonly ushort PoolSize;
			public readonly uint Index;
			Level _nextLevel;
			public Level NextLevel => LazyInitializer.EnsureInitialized(ref _nextLevel,
				() => new Level(Index + 1, Tower));

			readonly ConcurrentQueue<((TGenome Genome, Fitness[] Fitness) GenomeFitness, IFitness[] Scores, ushort LevelLossRecord)> Pool
				= new ConcurrentQueue<((TGenome Genome, Fitness[] Fitness) GenomeFitness, IFitness[] Scores, ushort LevelLossRecord)>();

			public Level(
				uint level,
				TowerProcessingScheme<TGenome> tower)
			{
				Index = level;
				Tower = tower;
				Factory = Tower.Factory[1]; // Use a lower priority than the factory used by broadcasting.

				var (First, Minimum, Step) = tower.PoolSize;
				var maxDelta = First - Minimum;
				var decrement = Index * Step;
				PoolSize = decrement > maxDelta ? Minimum : (ushort)(First - decrement);

				var probCount = tower.Problems.Count;
				BestLevelFitness = new IFitness[probCount];
				BestProgressiveFitness = new IFitness[probCount];
			}

			readonly IGenomeFactoryPriorityQueue<TGenome> Factory;


			readonly IFitness[] BestLevelFitness;
			readonly IFitness[] BestProgressiveFitness;


			static bool[] UpdateFitnessesIfBetter(IFitness[] registry, IEnumerable<IFitness> contending, bool useSnapShots)
			{
				Debug.Assert(registry != null);
				Debug.Assert(contending != null);
				var len = registry.Length;
				var result = new bool[len];
				var c = contending.GetEnumerator();

				var r = registry.AsSpan();
				for (var i = 0; i < len; i++)
				{
					c.MoveNext();
					var fitness = c.Current;
					ref IFitness fRef = ref r[i];
					while (fRef == null || fitness.IsSuperiorTo(fRef))
					{
						if (useSnapShots) fitness = fitness.SnapShot();
						var f = fRef;
						if (Interlocked.CompareExchange(ref fRef, fitness, f) == f)
						{
							result[i] = true;
							break;
						}
					}
				}

				return result;
			}


			(IFitness Fitness, (bool Local, bool Progressive, bool Both, bool Either) Superiority)[] ProcessTestAndUpdate((TGenome Genome, Fitness[] Fitness) c)
			{
				// Track local fitness
				var fitness = Tower.Problems.Select(p => p.ProcessTest(c.Genome, Index)).ToArray();
				var leveled = UpdateFitnessesIfBetter(BestLevelFitness, fitness, false);
				var progressed = UpdateFitnessesIfBetter(BestProgressiveFitness, c.Fitness.Select((f, i) => f.Merge(fitness[i])), true);
				return fitness.Select((f, i) =>
				{
					var lev = leveled[i];
					var pro = progressed[i];
					if (lev || pro)
					{
						Tower.Problems[i].ChampionPool?.Add(GenomeFitness.New(c.Genome, c.Fitness[i]));
						Factory.EnqueueChampion(c.Genome);
					}

					return (f, (lev, pro, lev && pro, lev || pro));
				}).ToArray();
			}

			public void Post(
				(TGenome Genome, Fitness[] Fitness) c,
				bool express = false,
				bool expressToTop = false)
			{
				// Process a test for this level.
				var result = ProcessTestAndUpdate(c);
				if (_nextLevel == null)
				{
					if (result.Any(r => r.Superiority.Progressive)) Tower.Broadcast(c);
				}
				else if (expressToTop || result.Any(r => r.Superiority.Local || express && r.Superiority.Progressive))
				{
					_nextLevel.Post(c, express, true); // expresssToTop... Since this is the reigning champ for this pool (or expressToTop).
					return; // No need to involve a obviously superior genome with this pool.
				}

				((TGenome Genome, Fitness[] Fitness) GenomeFitness, IFitness[] Scores, ushort LevelLossRecord) challenger
					= (c, result.Select(f => f.Fitness).ToArray(), 0);

				Pool.Enqueue(challenger);

				// Next see if we should 'own' processing the pool.
				if (Pool.Count >= PoolSize)
				{
					((TGenome Genome, Fitness[] Fitness) GenomeFitness, IFitness[] Scores, ushort LevelLossRecord)[] pool = null;
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
						var problemCount = Tower.Problems.Count;
						var selections = Enumerable.Range(0, problemCount)
							.Select(p => pool.OrderBy(e => e.Scores[p]).ToArray())
							.ToArray();

						// 2) Increment fitness rejection for individual fitnesses.
						for (var p = 0; p < problemCount; p++)
							for (var i = midPoint; i < len; i++)
								selections[p][i]
									.GenomeFitness
									.Fitness[p]
									.IncrementRejection();

						// 3) Weave all fitnesses and distinclty select the iwinners.
						var remaining = selections
							.Select(s => s.ToQueue().AsDequeueingEnumerable())
							.ToArray()
							.Weave()
							.Distinct();

						var winners = remaining.Take(midPoint).ToArray();

						// 4) Return losers to pool.
						var losersToPromote = new List<(TGenome Genome, Fitness[] Fitness)>();
						var maxLoses = Tower.MaxLevelLosses;
						var maxRejection = Tower.MaxLossesBeforeElimination;
						foreach (var loser in remaining)
						{
							var l = loser;
							l.LevelLossRecord++;
							var fitness = l.GenomeFitness.Fitness;

							if (l.LevelLossRecord > maxLoses)
							{
								if (fitness.Any(f => f.RejectionCount < maxRejection))
									losersToPromote.Add(l.GenomeFitness);
								else
								{
									//Host.Problem.Reject(loser.GenomeFitness.Genome.Hash);
									((GenomeFactoryBase<TGenome>)Tower.Factory).MetricsCounter.Increment("Genome Rejected");
								}
							}
							else
							{
								Debug.Assert(l.LevelLossRecord > 0);
								Pool.Enqueue(l);
							}
						}

						// 4) Save state for reuse.
						var lastLevel = _nextLevel == null;

						// 5) Promote winners.
						var top = winners[0].GenomeFitness;
						//Factory.EnqueueChampion(top.Genome);
						NextLevel.Post(top, true); // express: give it the opportunity to keep going.
						foreach (var winner in winners.Skip(1))
						{
							NextLevel.Post(winner.GenomeFitness);
						}

						// 6) Add top winners for each problem to their respective ranked pools.
						if (lastLevel)
						{
							for (var p = 0; p < problemCount; p++)
							{
								var gf = selections[p][0].GenomeFitness;
								Tower.Problems[p].ChampionPool?.Add((gf.Genome, gf.Fitness[p])); // Need to leverage potentially significant genetics...
							}
						}

						// 7) Promote second chance losers
						foreach (var loser in losersToPromote)
						{
							NextLevel.Post(loser);
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


		//readonly ConcurrentQueue<IGenomeFitness<TGenome, Fitness>> FastTrackQueue
		//	= new ConcurrentQueue<IGenomeFitness<TGenome, Fitness>>();		
		///// <summary>
		///// Process entry all the way to the last level before queueing.
		///// </summary>
		///// <param name="c"></param>
		//public void FastTrack(IGenomeFitness<TGenome, Fitness> c)
		//{
		//	if (_nextLevel == null)
		//	{
		//		//((GenomeFactoryBase<TGenome>)Tower.Environment.Factory).MetricsCounter.Increment("Fast Tracked");
		//		Post(c);
		//	}
		//	else
		//	{
		//		FastTrackQueue.Enqueue(c);
		//	}
		//}


		//public void ProcessPromotions()
		//{
		//	while (FastTrackQueue.TryDequeue(out IGenomeFitness<TGenome, Fitness> c))
		//	{
		//		// Process a test for this level.
		//		ProcessTestAndUpdate(c);
		//		_nextLevel.FastTrack(c);
		//	}
		//}

	}
}
