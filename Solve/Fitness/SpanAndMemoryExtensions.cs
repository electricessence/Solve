using Open.Numeric;
using System;
using System.Collections.Generic;

namespace Solve
{
	public static class SpanAndMemory<T>
		where T : IComparable<T>
	{
		class ComparerInternal : IComparer<ReadOnlyMemory<T>>
		{
			public ComparerInternal(int sign)
			{
				Sign = sign;
			}
			readonly int Sign;
			public int Compare(ReadOnlyMemory<T> x, ReadOnlyMemory<T> y) => Sign * SpanAndMemory<T>.Compare(x, y);
		}

		public static readonly IComparer<ReadOnlyMemory<T>> ComparerAscending = new ComparerInternal(+1);
		public static readonly IComparer<ReadOnlyMemory<T>> ComparerDescending = new ComparerInternal(-1);

		public static int Compare(in ReadOnlyMemory<T> target, in ReadOnlyMemory<T> other)
			=> Compare(target.Span, other.Span);

		public static int Compare(in ReadOnlySpan<T> target, in ReadOnlySpan<T> other)
		{
			var len = target.Length;
			if (len != other.Length)
				throw new ArgumentException("Lenths do not match.", nameof(other));

			for (var i = 0; i < len; i++)
			{
				var r = target[i].CompareTo(other[i]);
				if (r != 0) return r;
			}

			return 0;
		}
	}

	public static class SpanAndMemoryExtensions
	{


		public static int CompareTo<T>(this in ReadOnlyMemory<T> target, in ReadOnlyMemory<T> other)
			where T : IComparable<T>
			=> SpanAndMemory<T>.Compare(target.Span, other.Span);

		public static int CompareTo<T>(this in ReadOnlySpan<T> target, in ReadOnlySpan<T> other)
			where T : IComparable<T>
			=> SpanAndMemory<T>.Compare(target, other);

		public static bool IsLessThan<T>(this in ReadOnlySpan<T> target, in ReadOnlySpan<T> other)
			where T : IComparable<T>
			=> SpanAndMemory<T>.Compare(target, other) < 0;

		public static bool IsEqual<T>(this in ReadOnlySpan<T> target, in ReadOnlySpan<T> other)
			where T : IComparable<T>
			=> SpanAndMemory<T>.Compare(target, other) == 0;

		public static bool IsGreaterThan<T>(this in ReadOnlySpan<T> target, in ReadOnlySpan<T> other)
			where T : IComparable<T>
			=> SpanAndMemory<T>.Compare(target, other) > 0;


		public static bool IsLessThan<T>(this in ReadOnlyMemory<T> target, in ReadOnlyMemory<T> other)
			where T : IComparable<T>
			=> SpanAndMemory<T>.Compare(target, other) < 0;

		public static bool IsEqual<T>(this in ReadOnlyMemory<T> target, in ReadOnlyMemory<T> other)
			where T : IComparable<T>
			=> SpanAndMemory<T>.Compare(target, other) == 0;

		public static bool IsGreaterThan<T>(this in ReadOnlyMemory<T> target, in ReadOnlyMemory<T> other)
			where T : IComparable<T>
			=> SpanAndMemory<T>.Compare(target, other) > 0;

		public static ReadOnlyMemory<double> Averages(this in ReadOnlySpan<ProcedureResult> source)
		{
			var len = source.Length;
			var result = new double[len];
			for (var i = 0; i < len; i++)
				result[i] = source[i].Average;
			return result;
		}

	}
}
