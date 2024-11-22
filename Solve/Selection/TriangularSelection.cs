using Open.Arithmetic;
using System.Diagnostics;

namespace Solve.TriangularSelection;

public static class Ascending
{
	public static int GetRandomTriangularFavoredIndex(int length)
	{
		if (length > Triangular.MaxInt32)
			throw new ArgumentOutOfRangeException(nameof(length), length, $"Exceeds maximum Int32 value of {Triangular.MaxInt32}.");

		int possibilities = (int)Triangular.Forward(length);
		int selected = Random.Shared.Next(possibilities);
		int r = Triangular.Reverse(selected);
		Debug.Assert(r < length);
		return r;
	}

	public static T RandomOne<T>(IReadOnlyList<T> source)
		=> source[GetRandomTriangularFavoredIndex(source.Count)];

	public static T RandomOne<T>(in ReadOnlySpan<T> source)
		=> source[GetRandomTriangularFavoredIndex(source.Length)];
}

public static class Descending
{
	public static int GetRandomTriangularFavoredIndex(int length)
		=> length - Ascending.GetRandomTriangularFavoredIndex(length) - 1;

	public static T RandomOne<T>(IReadOnlyList<T> source)
		=> source[GetRandomTriangularFavoredIndex(source.Count)];

	public static T RandomOne<T>(in ReadOnlySpan<T> source)
		=> source[GetRandomTriangularFavoredIndex(source.Length)];
}
