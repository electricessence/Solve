using Open.Collections;
using Open.Memory;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes.Tower
{
	// ReSharper disable once PossibleInfiniteInheritance
	[SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
	public sealed partial class TowerProcessingScheme<TGenome>
	{
		sealed class Level : PostingTowerLevelBase<TGenome, ProblemTower>
		{
			Level? _nextLevel;
			public Level NextLevel => LazyInitializer.EnsureInitialized(ref _nextLevel,
				() => new Level(Index + 1, Tower));

			protected override bool IsTop => _nextLevel == null;

			private readonly BatchCreator<LevelEntry<TGenome>> Pool;

			public Level(
				int level,
				ProblemTower tower,
				byte priorityLevels = 4)
				: base(level, tower, priorityLevels)
			{
				Pool = new BatchCreator<LevelEntry<TGenome>>(PoolSize);
				Pool.BatchReady += Pool_BatchReady!;

				Processed = Enumerable
					.Range(0, priorityLevels)
					.Select(i => new ConcurrentQueue<LevelEntry<TGenome>>())
					.ToArray();
			}

			private void Pool_BatchReady(object sender, System.EventArgs e)
				=> Task.Run(() => ProcessPoolInternal());

			readonly ConcurrentQueue<LevelEntry<TGenome>>[] Processed;

			bool ProcessPoolInternal()
			{
				if (!Pool.TryDequeue(out var pool))
					return false;

				// 1) Setup selection.
				var len = pool.Length;
				var midPoint = pool.Length / 2;

				var promoted = new HashSet<string>();

				var problemPools = Tower.Problem.Pools;
				var problemPoolCount = problemPools.Count;
				var selection = RankEntries(pool);
				var isTop = _nextLevel == null;
				for (var i = 0; i < problemPoolCount; i++)
				{
					var s = selection[i];

					// 2) Signal & promote champions.
					var champ = s[0].GenomeFitness;

					if (isTop)
						ProcessChampion(i, champ);

					if (promoted.Add(champ.Genome.Hash))
						PostNextLevel(1, champ); // Champs may need to be posted synchronously to stay ahead of other deferred winners.

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
							PostNextLevel(2, winner); // PostStandby?
					}
				}

				//NextLevel.ProcessPool(true); // Prioritize winners and express.

				// 5) Process remaining (losers)
				var maxLoses = Tower.Environment.MaxLevelLosses;
				var maxRejection = Tower.Environment.MaxLossesBeforeElimination;
				foreach (var remainder in selection.Cast<IEnumerable<LevelEntry<TGenome>>>().Weave().Distinct())
				{
					if (promoted.Contains(remainder.GenomeFitness.Genome.Hash))
					{
						LevelEntry<TGenome>.Pool.Give(remainder);
						continue;
					}

					remainder.IncrementLoss();

					if (remainder.LevelLossRecord > maxLoses)
					{
						var gf = remainder.GenomeFitness;
						var fitnesses = gf.Fitness;
						if (fitnesses.Any(f => f.RejectionCount < maxRejection))
							PostNextLevel(3, gf);
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

						LevelEntry<TGenome>.Pool.Give(remainder);
					}
					else
					{
						Debug.Assert(remainder.LevelLossRecord > 0);
						Pool.Add(remainder); // Didn't win, but still in the game.
					}
				}

				foreach (var sel in selection) sel.Dispose();
				selection.Dispose();

				return true;
			}

			public void ProcessPool(bool thisLevelOnly = false)
			{
				while (ProcessPoolInternal()) { }

				if (thisLevelOnly) return;

				// walk up instead of recurse.
				Level? next = this;
				while ((next = next._nextLevel) != null)
					next.ProcessPool(true);
			}

			protected override void OnAfterPost()
			{
				base.OnAfterPost();

				int count;
				do
				{
					count = 0;
					var len = Processed.Length;
					for (var i = 0; i < len; i++)
					{
						var q = Processed[i];
						if (!q.TryDequeue(out var p)) continue;

						Pool.Add(p);

						i = -1; // Reset to top queue.
						++count;
					}
				}
				while (count != 0);
			}

			protected override void PostNextLevel(byte priority, (TGenome Genome, Fitness[] Fitness) challenger)
				=> NextLevel.Post(priority, challenger);

			void ProcessInjested(byte priority, LevelEntry<TGenome>? challenger)
			{
				if (challenger != null)
					Processed[priority].Enqueue(challenger);
			}

			protected override void ProcessInjested(byte priority, (TGenome Genome, Fitness[] Fitness) challenger)
			{
				var task = ProcessEntry(challenger);
				if (task.IsCompletedSuccessfully)
					ProcessInjested(priority, task.Result);
				else
				{
					Task.Run(async () =>
					{
						ProcessInjested(priority, await task);
					});
				}

			}
		}

	}
}
