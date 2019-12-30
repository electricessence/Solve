using Open.Memory;
using Solve.Supporting.TaskScheduling;
using System;
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
		sealed class Level : PostingTowerLevelBase<TGenome, ProblemTower>
		{
			Level? _nextLevel;
			private Level NextLevel => LazyInitializer.EnsureInitialized(ref _nextLevel,
				() => new Level(Index + 1, Tower));

			protected override bool IsTop => _nextLevel == null;

			public Level(
				int level,
				ProblemTower tower,
				byte priorityLevels = 4)
				: base(level, tower, priorityLevels)
			{
				var scheduler = tower.Scheduler[level];
				scheduler.Name = "Level Scheduler";
				scheduler.ReversePriority = true;

				PriorityQueueTaskScheduler GetScheduler(int pri, string name)
				{
					var s = scheduler[pri];
					s.Name = name;
					return s;
				}

				ExecutionDataflowBlockOptions SchedulerOption(int pri, string name, bool singleProducer = false)
					=> new ExecutionDataflowBlockOptions()
					{
						MaxDegreeOfParallelism = Environment.ProcessorCount,
						SingleProducerConstrained = singleProducer,
						TaskScheduler = GetScheduler(pri, name)
					};

				// Step 1: Group for ranking.
				var preselector = new BatchBlock<LevelEntry<TGenome>>(
					PoolSize,
					new GroupingDataflowBlockOptions() { TaskScheduler = GetScheduler(1, "Level Preselection") });

				// Step 2: Rank
				var ranking = new TransformBlock<LevelEntry<TGenome>[], TemporaryArray<TemporaryArray<LevelEntry<TGenome>>>>(
					e => RankEntries(e),
					SchedulerOption(2, "Level Ranking", true));

				// Step 3: Selection and Propagation!
				var selection = new ActionBlock<TemporaryArray<TemporaryArray<LevelEntry<TGenome>>>>(
					dataflowBlockOptions: SchedulerOption(3, "Level Selection", true),
					action: pools =>
					{
						var poolCount = pools.Length;
						var midPoint = PoolSize / 2;
						var promoted = new HashSet<string>();

						var isTop = _nextLevel == null;
						// Place champions first.
						{
							for (byte p = 0; p < poolCount; ++p)
							{
								var gf = pools[p][0].GenomeFitness;
								if (isTop)
									ProcessChampion(p, gf);
								if (promoted.Add(gf.Genome.Hash))
									PostNextLevel(1, gf);
							}
						}

						// Remaining top 50% (winners) should go before any losers.
						for (var i = 1; i < midPoint; ++i)
						{
							for (var p = 0; p < poolCount; ++p)
							{
								var gf = pools[p][i].GenomeFitness;
								if (promoted.Add(gf.Genome.Hash))
									PostNextLevel(2, gf);
							}
						}

						// Make sure losers have thier rejection count incremented.
						for (var i = midPoint; i < PoolSize; ++i)
						{
							for (var p = 0; p < poolCount; ++p)
							{
								pools[p][i].GenomeFitness.Fitness[p].IncrementRejection();
							}
						}

						// Distribute losers either back into the pool, pass them to the next level, or let them disapear (rejected).
						var maxLoses = Tower.Environment.MaxLevelLosses;
						var maxRejection = Tower.Environment.MaxLossesBeforeElimination;
						for (var i = midPoint; i < PoolSize; ++i)
						{
							for (var p = 0; p < poolCount; ++p)
							{
								var loser = pools[p][i];
								var gf = loser.GenomeFitness;
								loser.GenomeFitness.Fitness[p].IncrementRejection();
								if (!promoted.Add(gf.Genome.Hash)) continue;

								loser.IncrementLoss();
								if (loser.LevelLossRecord > maxLoses)
								{
									var fitnesses = gf.Fitness;
									if (fitnesses.Any(f => f.RejectionCount < maxRejection))
										PostNextLevel(3, gf);
									else if (Tower.Environment.Factory is GenomeFactoryBase<TGenome> f)
										f.MetricsCounter.Increment("Genome Rejected");
								}
								else
								{
									Debug.Assert(loser.LevelLossRecord > 0);
									preselector.Post(loser); // Didn't win, but still in the game.
								}

							}
						}

						foreach (var pool in pools) pool.Dispose();
						pools.Dispose();
					});

				// Step 0: Injestion
				Processor = new ActionBlock<(TGenome Genome, Fitness[] Fitness)>(
					dataflowBlockOptions: SchedulerOption(0, "Level Injestion"),
					action: async c =>
					{
						var result = await ProcessEntry(c);
						if (result != null && !preselector.Post(result))
							throw new Exception("Processor refused challenger.");
					});

				preselector.LinkTo(ranking);
				ranking.LinkTo(selection);
			}

			readonly ITargetBlock<(TGenome Genome, Fitness[] Fitness)> Processor;

			protected override void PostNextLevel(byte priority, (TGenome Genome, Fitness[] Fitness) challenger)
				=> NextLevel.Post(priority, challenger);

			protected override void ProcessInjested(byte priority, (TGenome Genome, Fitness[] Fitness) challenger)
			{
				if (!Processor.Post(challenger))
					throw new Exception("Processor refused challenger.");
			}

		}
	}
}
