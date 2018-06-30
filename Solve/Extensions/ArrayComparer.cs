using System;
using System.Collections.Generic;

namespace Solve
{
	public class ArrayComparer<T> : IComparer<T[]>
		where T : IComparable<T>
	{
		ArrayComparer(int sign = +1)
		{
			Sign = sign;
		}
		public readonly int Sign;
		public int Compare(T[] x, T[] y) => Sign * ArrayComparer.Compare(x, y);

		public static readonly IComparer<T[]> Ascending = new ArrayComparer<T>(+1);
		public static readonly IComparer<T[]> Descending = new ArrayComparer<T>(-1);
	}

	public static class ArrayComparer
	{
		public static int Compare<T>(in T[] target, in T[] other)
			where T : IComparable<T>
		{
			var len = target.Length;
			if (len != other.Length)
				throw new ArgumentException("Lengths do not match.");

			for (var i = 0; i < len; i++)
			{
				var r = target[i].CompareTo(other[i]);
				if (r != 0) return r;
			}

			return 0;
		}
	}
}
