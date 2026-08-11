namespace Solve.Tests;

public class TriangularSelectionTests
{
	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(10)]
	[InlineData(100)]
	public void AscendingIndexStaysInBounds(int length)
	{
		for (int i = 0; i < 1000; i++)
		{
			int index = Solve.TriangularSelection.Ascending.GetRandomTriangularFavoredIndex(length);
			Assert.InRange(index, 0, length - 1);
		}
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(10)]
	[InlineData(100)]
	public void DescendingIndexStaysInBounds(int length)
	{
		for (int i = 0; i < 1000; i++)
		{
			int index = Solve.TriangularSelection.Descending.GetRandomTriangularFavoredIndex(length);
			Assert.InRange(index, 0, length - 1);
		}
	}
}
