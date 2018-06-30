using System;

namespace Solve
{
	public static class SpanComparer
	{
		public static int Compare<T>(in Span<T> target, in Span<T> other)
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

		public static int Compare<T>(in ReadOnlySpan<T> target, in ReadOnlySpan<T> other)
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
