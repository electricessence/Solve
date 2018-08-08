using Open.Dataflow;
using Open.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks.Dataflow;

namespace Solve.ProcessingSchemes.Dataflow
{
	// ReSharper disable once PossibleInfiniteInheritance
	public partial class DataflowScheme<TGenome>
	{
		sealed class Level
		{
			readonly ProblemTower Tower;
			private readonly uint Index;
			Level _nextLevel;

			private Level NextLevel => LazyInitializer.EnsureInitialized(ref _nextLevel,
				() => new Level(Index + 1, Tower));

			public Level(
				uint level,
				ProblemTower tower,
				byte priorityLevels = 3)
			{
				Debug.Assert(level < tower.Environment.MaxLevels);

				Index = level;
				Tower = tower;
				var env = Tower.Environment;
				var factory = env.Factory[1];

				var (First, Minimum, Step) = env.PoolSize;
				var maxDelta = First - Minimum;
				var decrement = Index * Step;
				var poolSize = decrement > maxDelta ? Minimum : (ushort)(First - decrement);

				Incomming = Enumerable
					.Range(0, priorityLevels)
					.Select(i => new ConcurrentQueue<(TGenome Genome, Fitness[] Fitness)>())
					.ToArray();

				Processor = new TransformBlock<(TGenome Genome, Fitness[] Fitness), LevelEntry<TGenome>>(
					async c => LevelEntry.Merge(in c, (await Tower.Problem.ProcessSampleAsync(c.Genome, Index)).ToArray()));

				var preselection
					= new BatchBlock<LevelEntry<TGenome>>(poolSize);

				var selection
					= new TransformBlock<LevelEntry<TGenome>[], LevelEntry<TGenome>[][]>(pool =>
						Tower.Problem.Pools
							.Select((p, i) => pool.OrderBy(e => e.Scores[i], ArrayComparer<double>.Descending).ToArray())
							.ToArray());

				Processor.LinkTo(preselection);
				preselection.LinkTo(selection);
				selection.LinkTo(pools =>
				{
					var poolCount = pools.Length;
					var midPoint = poolSize / 2;
					var promoted = new HashSet<string>();

					var isTop = _nextLevel == null;
					// Place champions first.
					{
						var problemPools = Tower.Problem.Pools;
						for (var p = 0; p < poolCount; ++p)
						{
							var gf = pools[p][0].GenomeFitness;
							if (isTop)
							{
								problemPools[p].Champions?.Add(gf.Genome, gf.Fitness[p]);
								factory.EnqueueChampion(gf.Genome); // Okay to enqueue more than once if the same since it may be more significant.
								Tower.Broadcast(gf, p);
							}
							if (promoted.Add(gf.Genome.Hash))
								NextLevel.Post(0, gf);
						}
					}

					// Remaining top 50% (winners) should go before any losers.
					for (var i = 1; i < midPoint; ++i)
					{
						for (var p = 0; p < poolCount; ++p)
						{
							var gf = pools[p][i].GenomeFitness;
							if (promoted.Add(gf.Genome.Hash))
								NextLevel.Post(1, gf);
						}
					}

					// Make sure losers have thier rejection count incremented.
					for (var i = midPoint; i < poolSize; ++i)
					{
						for (var p = 0; p < poolCount; ++p)
						{
							pools[p][i].GenomeFitness.Fitness[p].IncrementRejection();
						}
					}

					// Distribute losers either back into the pool, pass them to the next level, or let them disapear (rejected).
					var maxLoses = Tower.Environment.MaxLevelLosses;
					var maxRejection = Tower.Environment.MaxLossesBeforeElimination;
					for (var i = midPoint; i < poolSize; ++i)
					{
						for (var p = 0; p < poolCount; ++p)
						{
							var loser = pools[p][i];
							var gf = loser.GenomeFitness;
							loser.GenomeFitness.Fitness[p].IncrementRejection();
							if (!promoted.Add(gf.Genome.Hash)) continue;

							loser.LevelLossRecord++;
							if (loser.LevelLossRecord > maxLoses)
							{
								var fitnesses = gf.Fitness;
								if (fitnesses.Any(f => f.RejectionCount < maxRejection))
									NextLevel.Post(2, gf);
								else if (Tower.Environment.Factory is GenomeFactoryBase<TGenome> f)
									f.MetricsCounter.Increment("Genome Rejected");
							}
							else
							{
								Debug.Assert(loser.LevelLossRecord > 0);
								preselection.Post(loser); // Didn't win, but still in the game.
							}

						}
					}
				});
			}

			readonly ConcurrentQueue<(TGenome Genome, Fitness[] Fitness)>[] Incomming;
			readonly TransformBlock<(TGenome Genome, Fitness[] Fitness), LevelEntry<TGenome>> Processor;

			public void Post(int priority, (TGenome Genome, Fitness[] Fitness) challenger)
			{
				Incomming[priority].Enqueue(challenger);

				int count;
				do
				{
					count = 0;
					var len = Incomming.Length;
					for (var i = 0; i < len; i++)
					{
						var q = Incomming[i];
						if (!q.TryDequeue(out var c)) continue;

						if (!Processor.Post(c))
							throw new Exception("Processor refused challenger.");
						i = -1; // Reset to top queue.
						++count;
					}
				}
				while (count != 0);
			}

		}
	}
}
