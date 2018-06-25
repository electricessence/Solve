using Open.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Solve.ProcessingSchemes
{
	public sealed partial class ExpressProcessingScheme<TGenome>
	{
		internal sealed class Level
		{
			readonly Tower Tower;
			public readonly ushort PoolSize;
			public readonly int Index;
			Level _nextLevel;
			public Level NextLevel => LazyInitializer.EnsureInitialized(ref _nextLevel,
				() => new Level(Index + 1, Tower));

			readonly ConcurrentQueue<((TGenome Genome, SampleFitnessCollectionBase Fitness) GenomeFitness, ReadOnlyMemory<double> Fitness, ushort LevelLossRecord)> Pool
				= new ConcurrentQueue<((TGenome Genome, SampleFitnessCollectionBase Fitness) GenomeFitness, ReadOnlyMemory<double> Fitness, ushort LevelLossRecord)>();

			public Level(
				int level,
				Tower host)
			{
				Index = level;
				Tower = host;
				Factory = Tower.Environment.Factory[1]; // Use a lower priority than the factory used by broadcasting.

				var (First, Minimum, Step) = host.Environment.PoolSize;
				var maxDelta = First - Minimum;
				var decrement = Index * Step;
				PoolSize = decrement > maxDelta ? Minimum : (ushort)(First - decrement);
			}

			readonly IGenomeFactoryPriorityQueue<TGenome> Factory;

			#region GenomeFitness Loss Record Comparer
			class GFLRComparer : Comparer<((TGenome Genome, SampleFitnessCollectionBase Fitness) GenomeFitness, ReadOnlyMemory<double> Fitness, ushort LevelLossRecord)>
			{
				public override int Compare(
					((TGenome Genome, SampleFitnessCollectionBase Fitness) GenomeFitness, ReadOnlyMemory<double> Fitness, ushort LevelLossRecord) x,
					((TGenome Genome, SampleFitnessCollectionBase Fitness) GenomeFitness, ReadOnlyMemory<double> Fitness, ushort LevelLossRecord) y)
					//=> GenomeFitness.Comparison(x.GenomeFitness, y.GenomeFitness);
					=> -x.Fitness.CompareTo(y.Fitness);

			}
			readonly static GFLRComparer _GFLRComparer = new GFLRComparer();
			#endregion

			readonly object BestLevelFitnessLock = new object();
			ReadOnlyMemory<double> BestLevelFitness = ReadOnlyMemory<double>.Empty;
			bool UpdateBestLevelFitnessIfBetter(ReadOnlyMemory<double> fitness)
			{
				ReadOnlyMemory<double> f;
				while ((f = BestLevelFitness).IsEmpty || fitness.IsGreaterThan(f))
				{
					lock (BestLevelFitnessLock)
					{
						if ((f = BestLevelFitness).IsEmpty || fitness.IsGreaterThan(f))
						{
							BestLevelFitness = fitness;
							return true;
						}
					}
				}
				return false;
			}

			readonly object BestProgressiveFitnessLock = new object();
			ReadOnlyMemory<double> BestProgressiveFitness = ReadOnlyMemory<double>.Empty;
			bool UpdateBestProgressiveFitnessIfBetter(ReadOnlyMemory<double> fitness)
			{
				ReadOnlyMemory<double> f;
				while ((f = BestProgressiveFitness).IsEmpty || fitness.IsGreaterThan(f))
				{
					lock (BestProgressiveFitnessLock)
					{
						if ((f = BestProgressiveFitness).IsEmpty || fitness.IsGreaterThan(f))
						{
							BestProgressiveFitness = fitness;
							return true;
						}
					}
				}
				return false;
			}

			(ReadOnlyMemory<double> Fitness, (bool Local, bool Progressive, bool Both, bool Either) Superiority) ProcessTestAndUpdate((TGenome Genome, SampleFitnessCollectionBase Fitness) c)
			{
				// Track local fitness
				var fitness = c.Fitness[Index];
				var leveled = UpdateBestLevelFitnessIfBetter(fitness);
				var progressed = UpdateBestProgressiveFitnessIfBetter(c.Fitness.Progression[Index].Span.Averages());

				if (leveled || progressed)
				{
					Tower.ChampionPool?.Add(c);
					Factory.EnqueueChampion(c.Genome);
				}

				return (fitness, (leveled, progressed, leveled && progressed, leveled || progressed));
			}

			public void Post(
				(TGenome Genome, SampleFitnessCollectionBase Fitness) c,
				bool express = false,
				bool expressToTop = false)
			{
				// Process a test for this level.
				var result = ProcessTestAndUpdate(c);
				if (_nextLevel == null)
				{
					if (result.Superiority.Progressive) Tower.Broadcast(c);
				}
				else if (expressToTop || result.Superiority.Local || express && result.Superiority.Progressive)
				{
					_nextLevel.Post(c, express, true); // expresssToTop... Since this is the reigning champ for this pool (or expressToTop).
					return; // No need to involve a obviously superior genome with this pool.
				}

				((TGenome Genome, SampleFitnessCollectionBase Fitness) GenomeFitness, ReadOnlyMemory<double> Fitness, ushort LevelLossRecord) challenger = (c, result.Fitness, 0);
				Pool.Enqueue(challenger);

				// Next see if we should 'own' processing the pool.
				if (Pool.Count >= PoolSize)
				{
					(IGenomeFitness<TGenome, Fitness> GenomeFitness, IFitness Fitness, ushort LevelLossRecord)[] selection = null;
					// If a lock is already aquired somewhere else, then skip/ignore...
					if (ThreadSafety.TryLock(Pool, () =>
					{
						if (Pool.Count >= PoolSize)
							selection = Pool.AsDequeueingEnumerable().Take(PoolSize).ToArray();
					}) && selection != null)
					{
						// 1) Sort by fitness.
						Array.Sort(selection, _GFLRComparer);

						// 2) Setup selection.
						var len = selection.Length;
						var midPoint = selection.Length / 2;

						// 3) Return losers to pool.
						var losersToPromote = new List<IGenomeFitness<TGenome, Fitness>>();
						for (var i = midPoint; i < len; i++)
						{
							var loser = selection[i];
							loser.LevelLossRecord++;
							var f = loser.GenomeFitness.Fitness;
							f.IncrementRejection();
							if (loser.LevelLossRecord > Tower.Environment.MaxLevelLosses)
							{
								if (f.RejectionCount < Tower.Environment.MaxLossesBeforeElimination)
									losersToPromote.Add(loser.GenomeFitness);
								//else
								//	Host.Problem.Reject(loser.GenomeFitness.Genome.Hash);
							}
							else
							{
								Pool.Enqueue(loser);
							}
						}

						// 4) Promote winners.
						var top = selection[0].GenomeFitness;
						//Factory.EnqueueChampion(top.Genome);
						NextLevel.Post(top, true); // express: give it the opportunity to keep going.
						var lastLevel = _nextLevel == null;
						for (var i = 1; i < midPoint; i++)
						{
							var n = selection[i].GenomeFitness;
							NextLevel.Post(n);
							if (lastLevel) Tower.ChampionPool?.Add(n); // Need to leverage potentially significant genetics...
						}

						// 5) Promote second chance losers
						foreach (var loser in losersToPromote)
						{
							NextLevel.Post(loser);
						}

						if (lastLevel)
						{
							Tower.Broadcast(top);
							//((GenomeFactoryBase<TGenome>)Tower.Environment.Factory).MetricsCounter.Increment("Top Level Pool Selected");
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
}
