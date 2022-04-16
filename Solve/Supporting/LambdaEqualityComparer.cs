using System;
using System.Collections.Generic;

namespace Solve;

public static class EqualityComparerUtility
{
	class Comparer<T> : EqualityComparer<T>
	{
		public Comparer(Func<T, T, bool> comparison, Func<T, int> hashGenerator)
		{
			_comparison = comparison;
			_hashGenerator = hashGenerator;
		}

		readonly Func<T, T, bool> _comparison;
		readonly Func<T, int> _hashGenerator;

		public override bool Equals(T? x, T? y) => x is null ? y is null : (y is not null && _comparison(x, y));
		public override int GetHashCode(T obj) => _hashGenerator(obj);
	}

	public static EqualityComparer<T> Create<T>(Func<T, T, bool> comparison, Func<T, int> hashGenerator)
		=> new Comparer<T>(comparison, hashGenerator);
}
