using Solve;

namespace Solve.Tests;

public class SplicedEnumerableTests
{
	private static char[] Source => "ABCDEFGH".ToCharArray();

	[Fact]
	public void Remove_AtIndex1_RemovesExactlyThatElement()
	{
		SplicedEnumerable<char> result = Source.SpliceAt(1).Remove(1);
		Assert.Equal("ACDEFGH", new string([.. result]));
		Assert.Equal(7, result.Count);
	}

	[Fact]
	public void Remove_LastElement_IsNotANoOp()
	{
		SplicedEnumerable<char> result = Source.SpliceAt(7).Remove(1);
		Assert.Equal("ABCDEFG", new string([.. result]));
		Assert.Equal(7, result.Count);
	}

	[Fact]
	public void Remove_ThreeMidElements()
	{
		SplicedEnumerable<char> result = Source.SpliceAt(2).Remove(3);
		Assert.Equal("ABFGH", new string([.. result]));
		Assert.Equal(5, result.Count);
	}

	[Fact]
	public void Remove_Negative_RemovesBackwardsFromSplice()
	{
		SplicedEnumerable<char> result = Source.SpliceAt(4).Remove(-2);
		Assert.Equal("ABEFGH", new string([.. result]));
		Assert.Equal(6, result.Count);
	}

	[Fact]
	public void Remove_AtIndex0()
	{
		SplicedEnumerable<char> result = Source.SpliceAt(0).Remove(1);
		Assert.Equal("BCDEFGH", new string([.. result]));
		Assert.Equal(7, result.Count);
	}

	[Fact]
	public void Remove_CountOvershootingTail_RemovesToEnd()
	{
		SplicedEnumerable<char> result = Source.SpliceAt(6).Remove(3);
		Assert.Equal("ABCDEF", new string([.. result]));
		Assert.Equal(6, result.Count);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public void Remove_CountAlwaysMatchesEnumeration(int removeCount)
	{
		char[] source = Source;
		for (int index = 0; index < source.Length; index++)
		{
			SplicedEnumerable<char> result = source.SpliceAt(index).Remove(removeCount);
			int enumerated = result.Count();
			Assert.Equal(result.Count, enumerated);
			// Remove(r) at index i deletes min(r, remaining-after-i) elements.
			int expected = source.Length - Math.Min(removeCount, source.Length - index);
			Assert.Equal(expected, enumerated);
		}
	}

	[Fact]
	public void Insert_AtIndex_PreservesOrderAndCount()
	{
		IEnumerable<char> result = Source.SpliceAt(3).Insert('X');
		Assert.Equal("ABCXDEFGH", new string([.. result]));
	}

	[Fact]
	public void MoveComposition_PreservesTotalLength()
	{
		// Mirrors Eater's "Move" mutation: remove at i, then insert the value at j.
		char[] source = Source;
		for (int i = 0; i < source.Length; i++)
		{
			char value = source[i];
			SplicedEnumerable<char> removed = source.SpliceAt(i).Remove(1);
			for (int j = 0; j < removed.Count; j++)
			{
				char[] moved = [.. removed.SpliceAt(j).Insert(value)];
				Assert.Equal(source.Length, moved.Length);
			}
		}
	}

	[Fact]
	public void Segments_BoundaryKeepsLastElement()
	{
		// splice at 3 of 8: head=ABC, tail=DEFGH. remove=4 → n=7=Count-1: last element must survive.
		(IEnumerable<char> head, IEnumerable<char> tail) = Source.SpliceAt(3).Segments(4);
		Assert.Equal("ABC", new string([.. head]));
		Assert.Equal("H", new string([.. tail]));
	}

	[Fact]
	public void Segments_RemoveToEnd_EmptyTail()
	{
		(IEnumerable<char> head, IEnumerable<char> tail) = Source.SpliceAt(3).Segments(5);
		Assert.Equal("ABC", new string([.. head]));
		Assert.Empty(tail);
	}
}
