using System;
using System.Collections.Generic;

namespace Solve
{
	public class MemoryComparer<T> : IComparer<Memory<T>>
		where T : IComparable<T>
	{
		MemoryComparer(int sign = +1)
		{
			Sign = sign;
		}
		public readonly int Sign;
		public int Compare(Memory<T> x, Memory<T> y) => Sign * MemoryComparer.Compare(x, y);

		public static readonly IComparer<Memory<T>> Ascending = new MemoryComparer<T>(+1);
		public static readonly IComparer<Memory<T>> Descending = new MemoryComparer<T>(-1);
	}

	public static class MemoryComparer
	{
		public static int Compare<T>(in Memory<T> target, in Memory<T> other)
			where T : IComparable<T>
			=> SpanComparer.Compare(target.Span, other.Span);
	}

	public class ReadOnlyMemoryComparer<T> : IComparer<ReadOnlyMemory<T>>
		where T : IComparable<T>
	{
		ReadOnlyMemoryComparer(int sign = +1)
		{
			Sign = sign;
		}
		readonly int Sign;
		public int Compare(ReadOnlyMemory<T> x, ReadOnlyMemory<T> y) => Sign * Compare(x, y);

		public static readonly IComparer<ReadOnlyMemory<T>> Ascending = new ReadOnlyMemoryComparer<T>(+1);
		public static readonly IComparer<ReadOnlyMemory<T>> Descending = new ReadOnlyMemoryComparer<T>(-1);

		public static int Compare(in ReadOnlyMemory<T> target, in ReadOnlyMemory<T> other)
			=> SpanComparer.Compare(target.Span, other.Span);
	}

}
