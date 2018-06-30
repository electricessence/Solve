using System;
using System.Text;

namespace Solve
{

	public static class ArrayExtensions
	{
		public static int CompareTo<T>(this T[] target, T[] other)
			where T : IComparable<T>
			=> ArrayComparer.Compare(target, other);

		public static int CompareTo<T>(this in ReadOnlyMemory<T> target, in ReadOnlyMemory<T> other)
			where T : IComparable<T>
			=> SpanComparer.Compare(target.Span, other.Span);

		public static int CompareTo<T>(this in ReadOnlySpan<T> target, in ReadOnlySpan<T> other)
			where T : IComparable<T>
			=> SpanComparer.Compare(target, other);


		public static bool IsLessThan<T>(this T[] target, T[] other)
			where T : IComparable<T>
			=> ArrayComparer.Compare(target, other) < 0;

		public static bool IsEqual<T>(this T[] target, T[] other)
			where T : IComparable<T>
			=> ArrayComparer.Compare(target, other) == 0;

		public static bool IsGreaterThan<T>(this T[] target, T[] other)
			where T : IComparable<T>
			=> ArrayComparer.Compare(target, other) > 0;


		public static bool IsLessThan<T>(this in ReadOnlySpan<T> target, in ReadOnlySpan<T> other)
			where T : IComparable<T>
			=> SpanComparer.Compare(target, other) < 0;

		public static bool IsEqual<T>(this in ReadOnlySpan<T> target, in ReadOnlySpan<T> other)
			where T : IComparable<T>
			=> SpanComparer.Compare(target, other) == 0;

		public static bool IsGreaterThan<T>(this in ReadOnlySpan<T> target, in ReadOnlySpan<T> other)
			where T : IComparable<T>
			=> SpanComparer.Compare(target, other) > 0;


		public static bool IsLessThan<T>(this in ReadOnlyMemory<T> target, in ReadOnlyMemory<T> other)
			where T : IComparable<T>
			=> SpanComparer.Compare(target.Span, other.Span) < 0;

		public static bool IsEqual<T>(this in ReadOnlyMemory<T> target, in ReadOnlyMemory<T> other)
			where T : IComparable<T>
			=> SpanComparer.Compare(target.Span, other.Span) == 0;

		public static bool IsGreaterThan<T>(this in ReadOnlyMemory<T> target, in ReadOnlyMemory<T> other)
			where T : IComparable<T>
			=> SpanComparer.Compare(target.Span, other.Span) > 0;

		public static StringBuilder ToStringBuilder<T>(this ReadOnlySpan<T> source)
		{
			var len = source.Length;
			var sb = new StringBuilder(len);

			for (var i = 0; i < len; i++)
			{
				sb.Append(source[i]);
			}

			return sb;
		}

		public static StringBuilder ToStringBuilder<T>(this in ReadOnlySpan<T> source, in string separator)
		{
			var len = source.Length;
			if (len < 2 || string.IsNullOrEmpty(separator))
				return ToStringBuilder(source);

			var sb = new StringBuilder(2 * len - 1);

			sb.Append(source[0]);
			for (var i = 1; i < len; i++)
			{
				sb.Append(separator);
				sb.Append(source[i]);
			}

			return sb;
		}

		public static StringBuilder ToStringBuilder<T>(this in ReadOnlySpan<T> source, in char separator)
		{
			var len = source.Length;
			if (len < 2)
				return ToStringBuilder(source);

			var sb = new StringBuilder(2 * len - 1);

			sb.Append(source[0]);
			for (var i = 1; i < len; i++)
			{
				sb.Append(separator);
				sb.Append(source[i]);
			}

			return sb;
		}
	}
}
