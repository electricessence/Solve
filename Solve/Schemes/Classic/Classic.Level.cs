using Open.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Solve.Schemes
{
	public sealed partial class Classic<TGenome>
	{
		sealed class Level
		{
			readonly Tournament Host;
			readonly ConcurrentQueue<(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LevelLossRecord)> Pool
				= new ConcurrentQueue<(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LevelLossRecord)>();
			readonly ConcurrentQueue<IGenomeFitness<TGenome, Fitness>> Promotions
				= new ConcurrentQueue<IGenomeFitness<TGenome, Fitness>>();
			readonly object PoolLock = new object();

			public readonly uint Index;
			Level _nextLevel;
			public Level NextLevel => LazyInitializer.EnsureInitialized(ref _nextLevel,
				() => new Level(Index + 1, Host));

			public Level(
				uint level,
				Tournament host)
			{
				Index = level;
				Host = host;
				FactoryPrimary = Host.Environment.Factory[0];
				//FactorySecondary = Host.Environment.Factory[1];
			}

			readonly IGenomeFactoryPriorityQueue<TGenome> FactoryPrimary;
			//readonly IGenomeFactoryPriorityQueue<TGenome> FactorySecondary;

			class GFLRComparer : Comparer<(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LevelLossRecord)>
			{
				public override int Compare((IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LevelLossRecord) x, (IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LevelLossRecord) y)
					=> GenomeFitness.Comparison(x.GenomeFitness, y.GenomeFitness);
			}

			readonly static GFLRComparer _GFLRComparer = new GFLRComparer();

			public void Post(IGenomeFitness<TGenome, Fitness> c)
			{
				var lastLevel = _nextLevel == null;
				// Process a test for this level.
				var fitness = Host.Problem.ProcessTest(c.Genome, Index);
				c.Fitness.Merge(fitness);

				(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LevelLossRecord) challenger = (c, 0);
				Pool.Enqueue(challenger);

				// Placing this here spreads out the load.
				ProcessNextPromotion();

				// Next see if we should 'own' processing the pool.
				var poolSize = Host.Environment.PoolSize;
				if (Pool.Count >= poolSize)
				{
					(IGenomeFitness<TGenome, Fitness> GenomeFitness, ushort LevelLossRecord)[] selection = null;
					// If a lock is already aquired somewhere else, then skip/ignore...
					if (ThreadSafety.TryLock(PoolLock, () =>
					{
						if (Pool.Count >= poolSize)
							selection = Pool.AsDequeueingEnumerable().Take(poolSize).ToArray();
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
							if (loser.LevelLossRecord > Host.Environment.MaxLevelLosses)
							{
								if (f.RejectionCount < Host.Environment.MaxLossesBeforeElimination)
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
						FactoryPrimary.EnqueueChampion(top.Genome);
						// Use the secondary queue for mutation here so that the champion get's priority.
						//FactorySecondary.EnqueueForVariation(top.Genome);
						//FactorySecondary.EnqueueForMutation(top.Genome);
						NextLevel.Promote(top); // Push this one to the finalist pool.
						for (var i = 1; i < midPoint; i++)
						{
							NextLevel.Post(selection[i].GenomeFitness);
						}

						// 5) Promote second chance losers
						foreach (var loser in losersToPromote)
						{
							NextLevel.Post(loser);
						}

						if (lastLevel)
						{
							Host.Broadcast(top);
						}

					}
				}

			}

			/// <summary>
			/// Process entry all the way to the last level before queueing.
			/// </summary>
			/// <param name="c"></param>
			public void Promote(IGenomeFitness<TGenome, Fitness> c)
			{
				if (_nextLevel == null)
					Post(c);
				else
					Promotions.Enqueue(c);
			}

			public void ProcessNextPromotion()
			{
				if (Promotions.TryDequeue(out IGenomeFitness<TGenome, Fitness> c))
				{
					// Process a test for this level.
					var fitness = Host.Problem.ProcessTest(c.Genome, Index);
					c.Fitness.Merge(fitness);
					_nextLevel.Promote(c);
				}
			}

		}
	}
}
