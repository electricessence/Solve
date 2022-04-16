using Open.Arithmetic;
using Open.RandomizationExtensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Solve.TriangularSelection;

public static class Ascending
{
	public static int GetRandomTriangularFavoredIndex(int length)
	{
		if (length > Triangular.MaxInt32)
			throw new ArgumentOutOfRangeException(nameof(length), length, $"Exceeds maximum Int32 value of {Triangular.MaxInt32}.");

		var possibilities = (int)Triangular.Forward(length);
		var selected = Randomizer.Random.Next(possibilities);
		var r = Triangular.Reverse(selected);
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
