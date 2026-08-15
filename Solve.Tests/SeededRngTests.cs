using Eater;
using Solve.Metrics;
using System.Collections.Concurrent;

namespace Solve.Tests;

/// <summary>
/// Verifies task 10-0002: an explicitly seeded <see cref="Random"/> injected into a
/// <see cref="GenomeFactory"/> reproduces a bit-identical sequence of generated genome hashes
/// across two independent factory instances, while different seeds diverge -- and that the
/// injected source survives concurrent access without corruption (see
/// <see cref="Solve.GenomeFactoryBase{TGenome}.RandomSource"/> for the locking strategy).
/// </summary>
public class SeededRngTests
{
	private static List<string> GenerateHashes(GenomeFactory factory, int successCount, int maxAttempts)
	{
		var hashes = new List<string>(successCount);
		int attempts = 0;
		while (hashes.Count < successCount && attempts < maxAttempts)
		{
			attempts++;
			if (factory.TryGenerateNew(out Genome? genome))
				hashes.Add(genome.Hash);
		}

		return hashes;
	}

	[Fact]
	public void SameSeedProducesIdenticalGenomeSequence()
	{
		var metrics = new CounterRegistry();
		var factoryA = new GenomeFactory(metrics, new Random(12345), seeds: null, leftTurnDisabled: true);
		var factoryB = new GenomeFactory(metrics, new Random(12345), seeds: null, leftTurnDisabled: true);

		List<string> hashesA = GenerateHashes(factoryA, 100, 5000);
		List<string> hashesB = GenerateHashes(factoryB, 100, 5000);

		// Sanity: both factories must have actually produced 100 genomes for the comparison
		// below to mean anything.
		Assert.Equal(100, hashesA.Count);
		Assert.Equal(100, hashesB.Count);

		Assert.Equal(hashesA, hashesB);
	}

	[Fact]
	public void DifferentSeedsProduceDifferentGenomeSequences()
	{
		var metrics = new CounterRegistry();
		var factoryA = new GenomeFactory(metrics, new Random(1), seeds: null, leftTurnDisabled: true);
		var factoryB = new GenomeFactory(metrics, new Random(2), seeds: null, leftTurnDisabled: true);

		List<string> hashesA = GenerateHashes(factoryA, 100, 5000);
		List<string> hashesB = GenerateHashes(factoryB, 100, 5000);

		Assert.Equal(100, hashesA.Count);
		Assert.Equal(100, hashesB.Count);

		Assert.NotEqual(hashesA, hashesB);
	}

	[Fact]
	public void UnseededFactoriesStillProduceValidGenomes()
	{
		// Default (no seed supplied) behavior must be unchanged: this should behave exactly
		// like the pre-existing, unseeded constructor overload.
		var metrics = new CounterRegistry();
		var factory = new GenomeFactory(metrics, seeds: null, leftTurnDisabled: true);

		List<string> hashes = GenerateHashes(factory, 50, 2000);

		Assert.Equal(50, hashes.Count);
		Assert.All(hashes, h => Assert.False(string.IsNullOrEmpty(h)));
	}

	[Fact]
	public void SeededRandomSourceIsThreadSafeUnderConcurrentDraws()
	{
		// Exercises the locking strategy documented on GenomeFactoryBase.RandomSource: a seeded
		// source must be safe to draw from concurrently -- every draw must complete without
		// throwing and land in the requested range, and none may be lost.
		var metrics = new CounterRegistry();
		var factory = new GenomeFactory(metrics, new Random(99), seeds: null, leftTurnDisabled: true);

		const int perThread = 2000;
		const int threadCount = 16;
		var results = new ConcurrentBag<int>();

		Parallel.For(0, threadCount, _ =>
		{
			for (int i = 0; i < perThread; i++)
				results.Add(factory.RandomSource.Next(1000));
		});

		Assert.Equal(perThread * threadCount, results.Count);
		Assert.All(results, v => Assert.InRange(v, 0, 999));
	}
}
