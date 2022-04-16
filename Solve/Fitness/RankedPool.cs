using Open.Collections;
using Open.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Solve;

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
	readonly ConcurrentQueue<(TGenome Genome, Fitness Fitness)> _pool = new();

	public bool IsEmpty => _pool.IsEmpty;

	Lazy<ImmutableArray<(TGenome Genome, Fitness Fitness)>>? _ranked;

	public void Add(TGenome genome, Fitness fitness)
	{
		if (_pool is null) return;

		_pool.Enqueue((genome, fitness));

		// Queue has changed.  Flush the last result.
		var rc = _ranked;
		if (rc is not null) Interlocked.CompareExchange(ref _ranked, null, rc);

		var count = _pool.Count;
		if (count <= PoolSize * 100) return;

		Debug.WriteLine($"Champion pool size reached: {count}");
		GetRanked(); // Overflowing?
	}

	ImmutableArray<(TGenome Genome, Fitness Fitness)> GetRanked()
		=> LazyInitializer.EnsureInitialized(ref _ranked,
			() => new Lazy<ImmutableArray<(TGenome Genome, Fitness Fitness)>>(() =>
		{
			var result = _pool
				// Drain queue (some*)
				.AsDequeueingEnumerable()
				// Eliminate duplicates
				.GroupBy(e => e.Genome.Hash)
				// Have enough to work with? (*)
				.Take(PoolSize * 2)
				.Select(e => // Setup ordering. Need to use snapshots for comparison.
				{
					var gf = e.First();
#if DEBUG
					var fitnessInstances = e.Select(f => f.Fitness).Distinct().ToArray();
					Debug.Assert(fitnessInstances.Length == 1);
#endif
					return (snapshot: gf.Fitness.Results, genomeFitness: (gf.Genome, gf.Fitness));
				})
				// Higher sample counts are more valuable as they only arrive here as champions.
				.OrderBy(e => e.snapshot.Average, CollectionComparer.Double.Descending)
				.ThenByDescending(e => e.snapshot.Count)
				// Compile results.
				.Select(e => e.genomeFitness)
				.ToImmutableArray();

			// (.Take(PoolSize)) All champions deserve a chance, but we will only retain the ones that can fit in the pool.				
			foreach (var e in result.Take(PoolSize)) _pool.Enqueue(e);
			return result;
		})).Value;

	public ImmutableArray<(TGenome Genome, Fitness Fitness)> Ranked => GetRanked();
}
