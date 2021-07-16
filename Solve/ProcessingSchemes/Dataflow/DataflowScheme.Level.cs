using Solve.Supporting.TaskScheduling;
using System;
using System.Threading;
using System.Threading.Tasks;
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
					=> new()
					{
						MaxDegreeOfParallelism = Environment.ProcessorCount,
						SingleProducerConstrained = singleProducer,
						TaskScheduler = GetScheduler(pri, name)
					};

				// Step 1: Group for ranking.
				var preselector = new BatchBlock<LevelEntry<TGenome>>(
					PoolSize,
					new GroupingDataflowBlockOptions() { TaskScheduler = GetScheduler(1, "Level Preselection") });

				Preselector = preselector;

				// Step 2: Rank
				var ranking = new TransformBlock<LevelEntry<TGenome>[], LevelEntry<TGenome>[][]>(
					RankEntries,
					SchedulerOption(2, "Level Ranking", true));

				// Step 3: Selection and Propagation!
				var selection = new ActionBlock<LevelEntry<TGenome>[][]>(
					dataflowBlockOptions: SchedulerOption(3, "Level Selection", true),
					action: async pools =>
					{
						await ProcessSelection(pools);
					});

				// Step 0: Injestion
				Processor = new ActionBlock<LevelProgress<TGenome>>(
					dataflowBlockOptions: SchedulerOption(0, "Level Injestion"),
					action: async c =>
					{
						var result = await ProcessEntry(c).ConfigureAwait(false);
						if (result != null && !preselector.Post(result))
							throw new Exception("Preselector refused challenger.");
					});

				preselector.LinkTo(ranking);
				ranking.LinkTo(selection);
			}

			readonly ITargetBlock<LevelEntry<TGenome>> Preselector;
			readonly ITargetBlock<LevelProgress<TGenome>> Processor;

			protected override ValueTask PostNextLevelAsync(byte priority, LevelProgress<TGenome> challenger)
				=> NextLevel.PostAsync(priority, challenger);

			protected override ValueTask PostThisLevelAsync(LevelEntry<TGenome> entry)
			{
				if (!Preselector.Post(entry))
					throw new Exception("Preselector refused challenger.");

				return new ValueTask();
			}

			protected override ValueTask ProcessInjested(byte priority, LevelProgress<TGenome> challenger)
			{
				if (!Processor.Post(challenger))
					throw new Exception("Processor refused challenger.");

				return new ValueTask();
			}

		}
	}
}
