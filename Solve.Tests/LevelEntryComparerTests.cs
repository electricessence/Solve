using Eater;
using Solve;
using Solve.ProcessingSchemes;
using System.Collections.Immutable;

namespace Solve.Tests;

public class LevelEntryComparerTests
{
	private static LevelEntry<Genome> Entry(params double[] score)
	{
		var progress = new LevelProgress<Genome>(Genome.Parse("3^>2^"), ImmutableArray<Fitness>.Empty);
		return LevelEntry<Genome>.Init(progress, [[.. score]], InterlockedInt.Init());
	}

	[Fact]
	public void SortWithNaNScoresDoesNotThrowAndRanksNaNLast()
	{
		IComparer<LevelEntry<Genome>> comparer = LevelEntry<Genome>.GetScoreComparer(0);
		LevelEntry<Genome>[] entries =
		[
			Entry(double.NaN, 1),
			Entry(1, 2),
			Entry(double.NaN, double.NaN),
			Entry(3, 0),
			Entry(1, double.NaN),
			Entry(2, 5),
		];

		Array.Sort(entries, comparer); // must not throw

		// Descending: real scores first (3, 2, 1...), NaN-led scores last.
		Assert.Equal(3, entries[0].Scores[0][0]);
		Assert.Equal(2, entries[1].Scores[0][0]);
		Assert.True(double.IsNaN(entries[^1].Scores[0][0]));
		Assert.True(double.IsNaN(entries[^2].Scores[0][0]));
	}

	[Fact]
	public void ComparerIsAntisymmetric()
	{
		IComparer<LevelEntry<Genome>> comparer = LevelEntry<Genome>.GetScoreComparer(0);
		LevelEntry<Genome>[] samples =
		[
			Entry(1, 2),
			Entry(2, 1),
			Entry(1, 2),
			Entry(double.NaN, 0),
			Entry(0, double.NaN),
			Entry(double.NaN, double.NaN),
		];

		foreach (LevelEntry<Genome> a in samples)
		{
			foreach (LevelEntry<Genome> b in samples)
			{
				int ab = comparer.Compare(a, b);
				int ba = comparer.Compare(b, a);
				Assert.Equal(Math.Sign(ab), -Math.Sign(ba));
			}
		}
	}

	[Fact]
	public void TieBreaksAreConsistentOverFullArraySort()
	{
		IComparer<LevelEntry<Genome>> comparer = LevelEntry<Genome>.GetScoreComparer(0);
		var random = new Random(42);
		LevelEntry<Genome>[] entries = [.. Enumerable.Range(0, 500).Select(_ =>
			Entry(
				random.Next(4) == 0 ? double.NaN : random.Next(3),
				random.Next(4) == 0 ? double.NaN : random.Next(3)))];

		Array.Sort(entries, comparer); // total order: never throws

		for (int i = 1; i < entries.Length; i++)
			Assert.True(comparer.Compare(entries[i - 1], entries[i]) <= 0);
	}
}
