using Solve.Metrics;

namespace Solve.Tests;

/// <summary>
/// Verifies task 15-0016: <see cref="GenomeFactoryBase{TGenome}.NoveltySaturation"/> (and its
/// underlying <see cref="GenomeFactoryMetrics.NoveltySaturation"/> ratio) rises when the
/// factory's generation/mutation/crossover operators repeatedly collide with an
/// already-registered genome -- the dedup rejection path in
/// <see cref="GenomeFactoryBase{TGenome}.RegisterProduction"/> -- rather than reading straight
/// zeros forever.
/// </summary>
public class NoveltySaturationTests
{
	/// <summary>
	/// The smallest possible <see cref="IGenome"/>: a fixed hash, freezable, nothing else. Used
	/// instead of a real problem's genome type (e.g. Eater's) so the test controls exactly which
	/// hashes the factory's operators produce, deterministically forcing collisions without
	/// depending on any concrete operator's randomized behavior happening to collide.
	/// </summary>
	private sealed class FakeGenome(string hash) : IGenome
	{
		public bool IsFrozen { get; private set; }
		public void Freeze() => IsFrozen = true;
		public int GeneCount => 1;
		public string Hash { get; } = hash;
		public object Clone() => new FakeGenome(Hash);

#if DEBUG
		public string StackTrace => string.Empty;
		public IReadOnlyList<IGenomeLogEntry> Log { get; } = [];
		public void AddLogEntry(string category, string message, string? data = null) { }
#endif
	}

	/// <summary>
	/// A factory whose operators are deliberately duplicate-prone: every generation always
	/// yields the same hash, every mutation always yields the same (different) hash, and every
	/// crossover always yields the same (yet another) hash. The very first attempt at each
	/// operator therefore registers successfully; every subsequent attempt collides with that
	/// already-registered genome and fails via <see cref="GenomeFactoryBase{TGenome}.RegisterProduction"/>.
	/// </summary>
	private sealed class DuplicateProneFactory(CounterRegistry metrics)
		: GenomeFactoryBase<FakeGenome>(metrics)
	{
		protected override FakeGenome GenerateOneInternal() => new("generated");
		protected override FakeGenome MutateInternal(FakeGenome target) => new("mutated");
		protected override FakeGenome[] CrossoverInternal(FakeGenome a, FakeGenome b) => [new FakeGenome("crossed")];
	}

	[Fact]
	public void NoveltySaturation_IsZero_BeforeAnyAttempts()
	{
		var factory = new DuplicateProneFactory(new CounterRegistry());

		Assert.Equal(0d, factory.NoveltySaturation);
		Assert.Equal(0d, factory.MetricsSnapshot.NoveltySaturation);
	}

	[Fact]
	public void NoveltySaturation_RisesWhenGenerationRepeatedlyRegistersTheSameGenome()
	{
		var factory = new DuplicateProneFactory(new CounterRegistry());

		// First call registers "generated" and succeeds -- ratio stays at zero.
		Assert.True(factory.TryGenerateNew(out FakeGenome? first));
		Assert.NotNull(first);
		Assert.Equal(0d, factory.NoveltySaturation);

		// Every subsequent call deliberately re-registers the same already-produced genome
		// (GenerateOneInternal always returns the "generated" hash) and must fail.
		const int duplicateAttempts = 4;
		for (int i = 0; i < duplicateAttempts; i++)
			Assert.False(factory.TryGenerateNew(out _));

		GenomeFactoryMetrics snapshot = factory.MetricsSnapshot;
		Assert.Equal(1, snapshot.GenerateNew.Succeeded);
		Assert.Equal(duplicateAttempts, snapshot.GenerateNew.Failed);

		double ratio = factory.NoveltySaturation;
		Assert.True(ratio > 0d, $"Expected a positive saturation ratio, got {ratio}.");
		Assert.Equal((double)duplicateAttempts / (duplicateAttempts + 1), ratio, precision: 10);
	}

	[Fact]
	public void NoveltySaturation_RisesWhenMutationsRepeatedlyCollideWithAnAlreadyRegisteredGenome()
	{
		var factory = new DuplicateProneFactory(new CounterRegistry());
		var source = new FakeGenome("seed");

		// First mutation attempt registers "mutated" and succeeds -- ratio stays at zero.
		Assert.True(factory.AttemptNewMutation(source, out FakeGenome? mutation));
		Assert.NotNull(mutation);
		double afterFirstSuccess = factory.NoveltySaturation;
		Assert.Equal(0d, afterFirstSuccess);

		// Every subsequent attempt against the same source collides with the same
		// already-registered "mutated" hash (MutateInternal always returns it) and fails.
		const int duplicateAttempts = 3;
		for (int i = 0; i < duplicateAttempts; i++)
			Assert.False(factory.AttemptNewMutation(source, out _));

		GenomeFactoryMetrics snapshot = factory.MetricsSnapshot;
		Assert.Equal(1, snapshot.Mutation.Succeeded);
		Assert.Equal(duplicateAttempts, snapshot.Mutation.Failed);

		double ratio = factory.NoveltySaturation;
		Assert.True(ratio > afterFirstSuccess, $"Expected saturation to rise above {afterFirstSuccess}, got {ratio}.");
	}

	[Fact]
	public void NoveltySaturation_BlendsAcrossGenerationMutationAndCrossover()
	{
		// Exercises all three operators the ratio is documented to span (task 15-0016 AC1/AC2),
		// and cross-checks that reading the ratio through the factory (as a caller like a unit
		// test would) agrees with reading it via an externally held CounterRegistry snapshot --
		// the same path Solve.Experiment.Console.RunnerBase.NoveltySaturation uses, since
		// RunnerBase never holds a reference to the factory itself, only the shared registry.
		var registry = new CounterRegistry();
		var factory = new DuplicateProneFactory(registry);

		Assert.True(factory.TryGenerateNew(out _)); // "generated" succeeds
		Assert.True(factory.AttemptNewMutation(new FakeGenome("seed"), out _)); // "mutated" succeeds

		var a1 = new FakeGenome("a1");
		var b1 = new FakeGenome("b1");
		Assert.Single(factory.AttemptNewCrossover(a1, b1)); // "crossed" succeeds

		Assert.Equal(0d, factory.NoveltySaturation);

		// Now collide all three: duplicate generation, duplicate mutation, and a crossover
		// between a fresh (distinct) pair that still produces the already-registered "crossed"
		// hash.
		Assert.False(factory.TryGenerateNew(out _));
		Assert.False(factory.AttemptNewMutation(new FakeGenome("seed"), out _));

		var a2 = new FakeGenome("a2");
		var b2 = new FakeGenome("b2");
		Assert.Empty(factory.AttemptNewCrossover(a2, b2));

		double viaFactory = factory.NoveltySaturation;
		double viaExternalSnapshot = GenomeFactoryMetrics.Get(registry.Snapshot()).NoveltySaturation;

		Assert.True(viaFactory > 0d, $"Expected a positive saturation ratio, got {viaFactory}.");
		Assert.Equal(viaExternalSnapshot, viaFactory, precision: 10);
		// 3 successes, 3 failures across the three operators combined.
		Assert.Equal(0.5d, viaFactory, precision: 10);
	}
}
