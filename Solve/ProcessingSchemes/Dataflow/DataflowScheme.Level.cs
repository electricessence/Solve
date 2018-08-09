﻿using Open.Dataflow;
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
		sealed class Level : TowerLevelBase<TGenome, ProblemTower>
		{
			Level _nextLevel;
			private Level NextLevel => LazyInitializer.EnsureInitialized(ref _nextLevel,
				() => new Level(Index + 1, Tower));

			protected override bool IsTop => _nextLevel == null;

			public Level(
				uint level,
				ProblemTower tower,
				byte priorityLevels = 4)
				: base(level, tower, priorityLevels)
			{
				var preselector
					= new BatchBlock<LevelEntry>(PoolSize);

				var selection
					= new TransformBlock<LevelEntry[], LevelEntry[][]>(e => RankEntries(e));

				preselector.LinkTo(selection);
				selection.LinkTo(pools =>
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

							loser.LevelLossRecord++;
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
				});

				Processor = new ActionBlock<(TGenome Genome, Fitness[] Fitness)>(async c =>
				{
					var result = await ProcessEntry(c);
					if (result != null && !preselector.Post(result))
						throw new Exception("Processor refused challenger.");
				});

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
