using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Solve
{
	public class RankedPool<TGenome>
		where TGenome : IGenome
	{
		public RankedPool(ushort poolSize)
		{
			if (poolSize < 2)
				throw new ArgumentOutOfRangeException(nameof(poolSize), poolSize, "Must be at least 2.");
			PoolSize = poolSize;
		}

		public readonly ushort PoolSize;
		readonly ConcurrentQueue<(TGenome Genome, FitnessContainer Fitness)> _pool
			= new ConcurrentQueue<(TGenome Genome, FitnessContainer Fitness)>();

		Lazy<TGenome[]> _ranked;

		public void Add((TGenome Genome, FitnessContainer Fitness) genome)
		{
			if (_pool == null) return;

			_pool.Enqueue(genome);

			// Queue has changed.  Flush the last result.
			var rc = _ranked;
			if (rc != null) Interlocked.CompareExchange(ref _ranked, null, rc);

			var count = _pool.Count;
			if (count > PoolSize * 100)
			{
				Debug.WriteLine($"Champion pool size reached: {count}");
				GetRanked(); // Overflowing?
			}
		}

		TGenome[] GetRanked()
			=> LazyInitializer.EnsureInitialized(ref _ranked, () => new Lazy<TGenome[]>(() =>
			{
				var result = _pool
					// Drain queue (some*)
					.AsDequeueingEnumerable()
					// Eliminate duplicates
					.Distinct()
					// Have enough to work with? (*)
					.Take(PoolSize * 2)
					// Setup ordering. Need to use snapshots for comparison.
					.Select(e => (snapshot: e.Fitness.Results, genomeFitness: e))
					// Higher sample counts are more valuable as they only arrive here as champions.
					.OrderBy(e => e.snapshot.Average, ReadOnlyMemoryComparer<double>.Descending)
					.ThenByDescending(e => e.snapshot.Count)
					// Compile results.
					.Select(e => e.genomeFitness)
					.ToArray();

				// (.Take(ChampionPoolSize)) All champions deserve a chance, but we will only retain the ones that can fit in the pool.				
				foreach (var e in result.Take(PoolSize)) _pool.Enqueue(e);
				return result.Select(g => g.Genome).ToArray();
			})).Value;

		public ReadOnlySpan<TGenome> Ranked
			=> GetRanked();
	}
}
