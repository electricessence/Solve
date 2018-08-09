using Open.Collections;
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
		sealed class Level : TowerLevelBase<TGenome, ProblemTower>
		{
			Level _nextLevel;
			public Level NextLevel => LazyInitializer.EnsureInitialized(ref _nextLevel,
				() => new Level(Index + 1, Tower));

			protected override bool IsTop => _nextLevel == null;

			private readonly BatchCreator<LevelEntry> Pool;

			public Level(
				uint level,
				ProblemTower tower,
				byte priorityLevels = 4)
				: base(level, tower, priorityLevels)
			{
				Pool = new BatchCreator<LevelEntry>(PoolSize);
				Pool.BatchReady += Pool_BatchReady;

				Processed = Enumerable
					.Range(0, priorityLevels)
					.Select(i => new ConcurrentQueue<LevelEntry>())
					.ToArray();
			}

			private void Pool_BatchReady(object sender, System.EventArgs e)
				=> Task.Run(() => ProcessPoolInternal());

			readonly ConcurrentQueue<LevelEntry>[] Processed;

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
				for (byte i = 0; i < problemPoolCount; i++)
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
				foreach (var loser in selection.Select(s => s.Skip(midPoint)).Weave().Distinct())
				{
					if (promoted.Contains(loser.GenomeFitness.Genome.Hash)) continue;
					loser.LevelLossRecord++;

					if (loser.LevelLossRecord > maxLoses)
					{
						var gf = loser.GenomeFitness;
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
					}
					else
					{
						Debug.Assert(loser.LevelLossRecord > 0);
						Pool.Add(loser); // Didn't win, but still in the game.
					}
				}
				return true;
			}

			public void ProcessPool(bool thisLevelOnly = false)
			{
				while (ProcessPoolInternal()) { }

				if (thisLevelOnly) return;

				// walk up instead of recurse.
				var next = this;
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

			protected override void ProcessInjested(byte priority, (TGenome Genome, Fitness[] Fitness) challenger)
			{
				ProcessEntry(challenger).ContinueWith(e =>
				{
					var result = e.Result;
					if (result != null)
						Processed[priority].Enqueue(result);
				});
			}
		}

	}
}
