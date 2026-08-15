using Eater;
using Solve.Metrics;
using System.Diagnostics;

namespace Solve.Tests;

public class FactoryQueueTests
{
	[Fact]
	public void ProcessBreederIsBounded()
	{
		var metrics = new CounterRegistry();
		var factory = new GenomeFactory(metrics, seeds: null, leftTurnDisabled: true);
		IGenomeFactoryPriorityQueue<Genome> queue = factory[0];

		// DISTINCT hashes are essential: they route BreedOne into the real
		// crossover branch, which is what the per-call work cap governs.
		// (Identical hashes drain in one O(n) consolidation pass instead.)
		foreach (string hash in GenomeFactory.Random(10, 10, leftTurnDisabled: true).Take(50_000).Distinct())
			queue.EnqueueForBreeding(Genome.Parse(hash));

		var stopwatch = Stopwatch.StartNew();
		queue.TryGetNext(out Genome? _);
		stopwatch.Stop();

		// Capped code path performs ≤64 breed attempts (near-instant); the old
		// cubic formula at 50k stock would attempt ~132k crossovers (minutes).
		Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15),
			$"TryGetNext took {stopwatch.Elapsed} — breeding work is not bounded.");
	}
}
