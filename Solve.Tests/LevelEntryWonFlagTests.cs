using Eater;
using Solve.ProcessingSchemes;
using System.Collections.Immutable;

namespace Solve.Tests;

public class LevelEntryWonFlagTests
{
	private static LevelProgress<Genome> Progress()
		=> new(Genome.Parse("3^>2^"), ImmutableArray<Fitness>.Empty);

	[Fact]
	public void Init_DefaultsWonToFalse()
	{
		LevelEntry<Genome> entry = LevelEntry<Genome>.Init(
			Progress(), [ImmutableArray.Create(1.0)], InterlockedInt.Init());

		Assert.False(entry.Won);
	}

	[Fact]
	public void Init_SetsWonWhenSpecified()
	{
		LevelEntry<Genome> entry = LevelEntry<Genome>.Init(
			Progress(), [ImmutableArray.Create(1.0)], InterlockedInt.Init(), won: true);

		Assert.True(entry.Won);
	}

	[Fact]
	public void Recycle_ResetsWonToFalse()
	{
		LevelEntry<Genome> entry = LevelEntry<Genome>.Init(
			Progress(), [ImmutableArray.Create(1.0)], InterlockedInt.Init(), won: true);
		Assert.True(entry.Won);

		entry.Recycle();

		Assert.False(entry.Won);
	}

	[Fact]
	public void PooledReuse_DoesNotLeakWonFlagAcrossRecycle()
	{
		// A pooled entry that previously won must not silently report Won for the
		// next (unrelated) genome that reuses the same pooled instance.
		LevelEntry<Genome> first = LevelEntry<Genome>.Init(
			Progress(), [ImmutableArray.Create(1.0)], InterlockedInt.Init(), won: true);
		LevelEntry<Genome>.Pool.Give(first);

		LevelEntry<Genome> second = LevelEntry<Genome>.Init(
			Progress(), [ImmutableArray.Create(1.0)], InterlockedInt.Init(), won: false);

		Assert.False(second.Won);
	}
}
