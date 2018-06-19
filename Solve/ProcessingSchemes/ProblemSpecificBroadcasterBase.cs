using Open.Dataflow;
using Solve.Metrics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	public abstract class ProblemSpecificBroadcasterBase<TGenome>
		: BroadcasterBase<IGenomeFitness<TGenome>>
		where TGenome : class, IGenome
	{
		protected ProblemSpecificBroadcasterBase(
			IGenomeFactory<TGenome> genomeFactory,
			ushort championPoolSize,
			CounterCollection counterCollection = null)
		{
			if(genomeFactory==null) throw new ArgumentNullException(nameof(genomeFactory));
			ChampionPoolSize = championPoolSize;
			if (championPoolSize != 0)
			{
				Champions = new ConcurrentQueue<IGenomeFitness<TGenome>>();
				FactoryReserve = genomeFactory[2];
				FactoryReserve.ExternalProducers.Add(ProduceFromChampions);
			}
			Counters = counterCollection;
		}

		protected readonly CounterCollection Counters;

		#region Champion Pool

		public readonly ushort ChampionPoolSize;
		protected readonly IGenomeFactoryPriorityQueue<TGenome> FactoryReserve;
		readonly ConcurrentQueue<IGenomeFitness<TGenome>> Champions;
		Lazy<TGenome[]> RankedChampions;

		public void AddChampion(IGenomeFitness<TGenome> genome)
		{
			Debug.Assert(genome != null);
			if (Champions == null || genome == null) return;

			Champions.Enqueue(genome);

			// Queue has changed.  Flush the last result.
			var rc = RankedChampions;
			if (rc != null) Interlocked.CompareExchange(ref RankedChampions, null, rc);

			var count = Champions.Count;
			if (count > ChampionPoolSize * 100)
			{
				Debug.WriteLine($"Champion pool size reached: {count}");
				GetRankedChampions(); // Overflowing?
			}
		}

		TGenome[] GetRankedChampions()
			=> LazyInitializer.EnsureInitialized(ref RankedChampions, () => new Lazy<TGenome[]>(() =>
			{
				var result = Champions
					// Drain queue (some*)
					.AsDequeueingEnumerable()
					// Eliminate duplicates
					.Distinct()
					// Have enough to work with? (*)
					.Take(ChampionPoolSize * 2)
					// Setup ordering. Need to use snapshots for comparison.
					.Select(e => (snapshot: e.Fitness.SnapShot(), genomeFitness: e))
					// Higher sample counts are more valuable as they only arrive here as champions.
					.OrderByDescending(e => e.snapshot.SampleCount)
					.ThenBy(e => e.snapshot)
					// Compile results.
					.Select(e => e.genomeFitness)
					.ToArray();

#if DEBUG
				Counters?.Increment("Champion Ranking");
#endif

				// (.Take(ChampionPoolSize)) All champions deserve a chance, but we will only retain the ones that can fit in the pool.				
				foreach (var e in result.Take(ChampionPoolSize)) Champions.Enqueue(e);
				return result.Select(g => g.Genome).ToArray();
			})).Value;

		protected bool ProduceFromChampions()
		{
			if (Champions == null) return false;

			var champions = GetRankedChampions();
			var len = champions.Length;
			if (champions.Length > 0)
			{
				FactoryReserve.EnqueueForMutation(champions[0], champions[0]);
				if (champions.Length > 1)
				{
					FactoryReserve.EnqueueForMutation(champions[1]);
					FactoryReserve.Breed(champions);
				}
#if DEBUG
				Counters?.Increment("Champion Ranking Production");
#endif
				return true;
			}

			return false;
		}

		#endregion

	}
}
