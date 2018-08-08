using Open.Collections;
using Open.Memory;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	// ReSharper disable once PossibleInfiniteInheritance
	[SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
	public sealed partial class TowerProcessingScheme<TGenome>
	{
		sealed class Level
		{
			readonly ProblemTower Tower;
			public readonly ushort PoolSize;
			public readonly uint Index;
			Level _nextLevel;
			public Level NextLevel => LazyInitializer.EnsureInitialized(ref _nextLevel,
				() => new Level(Index + 1, Tower));

			private readonly BatchCreator<LevelEntry<TGenome>> Pool;

			public Level(
				uint level,
				ProblemTower tower,
				byte priorityLevels = 3)
			{
				Debug.Assert(level < tower.Environment.MaxLevels);

				Index = level;
				Tower = tower;
				var env = Tower.Environment;
				Factory = env.Factory[1]; // Use a lower priority than the factory used by broadcasting.

				var (First, Minimum, Step) = env.PoolSize;
				var maxDelta = First - Minimum;
				var decrement = Index * Step;

				PoolSize = decrement > maxDelta ? Minimum : (ushort)(First - decrement);
				Pool = new BatchCreator<LevelEntry<TGenome>>(PoolSize);
				Pool.BatchReady += Pool_BatchReady;

				Incomming = Enumerable
					.Range(0, priorityLevels)
					.Select(i => new ConcurrentQueue<(TGenome Genome, Fitness[] Fitness)>())
					.ToArray();

				Processed = Enumerable
					.Range(0, priorityLevels)
					.Select(i => new ConcurrentQueue<LevelEntry<TGenome>>())
					.ToArray();
			}

			private void Pool_BatchReady(object sender, System.EventArgs e)
				=> Task.Run(() => ProcessPoolInternal());


			//public readonly bool IsMaxLevel;
			readonly IGenomeFactoryPriorityQueue<TGenome> Factory;

			readonly ConcurrentQueue<(TGenome Genome, Fitness[] Fitness)>[] Incomming;
			readonly ConcurrentQueue<LevelEntry<TGenome>>[] Processed;

			async Task<LevelEntry<TGenome>> ProcessEntry((TGenome Genome, Fitness[] Fitness) c)
				=> LevelEntry.Merge(in c, (await Tower.Problem.ProcessSampleAsync(c.Genome, Index)).ToArray());


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
				var selection = new LevelEntry<TGenome>[problemPoolCount][];
				var isTop = _nextLevel == null;
				for (var i = 0; i < problemPoolCount; i++)
				{
					var p = problemPools[i];
					var s = pool
						.OrderBy(e => e.Scores[i], ArrayComparer<double>.Descending)
						.ToArray();
					selection[i] = s;

					// 2) Signal & promote champions.
					var champ = s[0].GenomeFitness;

					if (isTop)
					{
						Factory.EnqueueChampion(champ.Genome);
						p.Champions?.Add(champ.Genome, champ.Fitness[i]);
						Tower.Broadcast(champ, i);
					}

					if (promoted.Add(champ.Genome.Hash))
					{
						NextLevel.Post(0, champ); // Champs may need to be posted synchronously to stay ahead of other deferred winners.
					}


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
							NextLevel.Post(1, winner); // PostStandby?
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
							NextLevel.Post(2, gf);
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

						var p = Processed[i];
						ProcessEntry(c).ContinueWith(e => p.Enqueue(e.Result));

						i = -1; // Reset to top queue.
						++count;
					}
				}
				while (count != 0);

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

		}

	}
}
