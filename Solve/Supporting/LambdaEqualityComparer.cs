namespace Solve;

public static class EqualityComparerUtility
{
	private sealed class Comparer<T>(Func<T, T, bool> comparison, Func<T, int> hashGenerator) : EqualityComparer<T>
	{
		public override bool Equals(T? x, T? y) => x is null ? y is null : (y is not null && comparison(x, y));
		public override int GetHashCode(T obj) => hashGenerator(obj);
	}

	public static EqualityComparer<T> Create<T>(Func<T, T, bool> comparison, Func<T, int> hashGenerator)
		=> new Comparer<T>(comparison, hashGenerator);
}
