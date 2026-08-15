using Open.Arithmetic;
using System.Diagnostics;

namespace Solve.TriangularSelection;

public static class Ascending
{
	// 10-0002: parameterless overloads keep drawing from Random.Shared (unchanged default
	// behavior for existing callers); the new Random-accepting overloads let a caller supply
	// its own (potentially seeded) source for reproducible selection.
	public static int GetRandomTriangularFavoredIndex(int length)
		=> GetRandomTriangularFavoredIndex(length, Random.Shared);

	public static int GetRandomTriangularFavoredIndex(int length, Random random)
	{
		ArgumentNullException.ThrowIfNull(random);
		if (length > Triangular.MaxInt32)
			throw new ArgumentOutOfRangeException(nameof(length), length, $"Exceeds maximum Int32 value of {Triangular.MaxInt32}.");

		int possibilities = (int)Triangular.Forward(length);
		int selected = random.Next(possibilities);
		int r = Triangular.Reverse(selected);
		Debug.Assert(r < length);
		return r;
	}

	public static T RandomOne<T>(IReadOnlyList<T> source)
		=> source[GetRandomTriangularFavoredIndex(source.Count)];

	public static T RandomOne<T>(IReadOnlyList<T> source, Random random)
		=> source[GetRandomTriangularFavoredIndex(source.Count, random)];

	public static T RandomOne<T>(in ReadOnlySpan<T> source)
		=> source[GetRandomTriangularFavoredIndex(source.Length)];

	public static T RandomOne<T>(in ReadOnlySpan<T> source, Random random)
		=> source[GetRandomTriangularFavoredIndex(source.Length, random)];
}

public static class Descending
{
	public static int GetRandomTriangularFavoredIndex(int length)
		=> length - Ascending.GetRandomTriangularFavoredIndex(length) - 1;

	public static int GetRandomTriangularFavoredIndex(int length, Random random)
		=> length - Ascending.GetRandomTriangularFavoredIndex(length, random) - 1;

	public static T RandomOne<T>(IReadOnlyList<T> source)
		=> source[GetRandomTriangularFavoredIndex(source.Count)];

	public static T RandomOne<T>(IReadOnlyList<T> source, Random random)
		=> source[GetRandomTriangularFavoredIndex(source.Count, random)];

	public static T RandomOne<T>(in ReadOnlySpan<T> source)
		=> source[GetRandomTriangularFavoredIndex(source.Length)];

	public static T RandomOne<T>(in ReadOnlySpan<T> source, Random random)
		=> source[GetRandomTriangularFavoredIndex(source.Length, random)];
}
