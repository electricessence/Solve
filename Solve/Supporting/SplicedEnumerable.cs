using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Solve
{

	public struct SplicedEnumerable<T> : IReadOnlyCollection<T>
	{
		private readonly IEnumerable<T> _head;
		private readonly IEnumerable<T> _tail;
		public int SpliceIndex { get; }
		public int Count { get; }

		private static readonly IEnumerable<T> Empty = Enumerable.Empty<T>();

		internal SplicedEnumerable(IEnumerable<T> head, IEnumerable<T> tail, int index, int length)
		{
			if (length < 0) throw new ArgumentOutOfRangeException(nameof(length), length, "Must be at least zero.");
			_head = head ?? throw new ArgumentNullException(nameof(head));
			_tail = tail ?? throw new ArgumentNullException(nameof(tail));
			Count = length;
			SpliceIndex = index < 0 ? 0 : Math.Min(index, length - 1);
		}

		internal SplicedEnumerable(IReadOnlyCollection<T> source, int index, int? length = null)
		{
			Count = length ?? source.Count;
			if (Count < 0) throw new ArgumentOutOfRangeException(nameof(length), length, "Must be at least zero.");

			SpliceIndex = index < 0 ? 0 : Math.Min(index, Count - 1);
			if (SpliceIndex > 0)
			{
				if (SpliceIndex < Count - 1)
				{
					// In bounds..
					_head = source.Take(index);
					_tail = source.Skip(index);
				}
				else
				{
					_head = source;
					_tail = Empty;
				}
			}
			else
			{
				_head = Empty;
				_tail = source;
			}
		}

		public (IEnumerable<T> head, IEnumerable<T> tail) Segments(int remove = 0)
		{
			if (remove == 0) return (_head, _tail);
			var n = SpliceIndex + remove;
			if (n <= 0) return (Empty, _tail);
			if (n >= Count - 1) return (_head, Empty);

			if (remove < 0) return (_head.Take(n), _tail);
			return (_head, _tail.Skip(remove));
		}

		public SplicedEnumerable<T> Remove(int count)
		{
			if (count == 0) return this;

			if (count > 0)
			{
				var tailLen = Math.Max(0, Count - SpliceIndex - count);
				var tail = tailLen > 0 ? _tail.Skip(tailLen) : Empty;
				return new SplicedEnumerable<T>(_head, tail, SpliceIndex, SpliceIndex + tailLen);
			}

			var headLen = Math.Max(0, SpliceIndex + count);
			var head = headLen > 0 ? _head.Take(headLen) : Empty;
			return new SplicedEnumerable<T>(head, _tail, headLen, Count - SpliceIndex + headLen);
		}

		public SplicedEnumerable<T> InsertSegment(IReadOnlyCollection<T> e, bool shiftIndex = false)
		{
			var len = e.Count;
			return new SplicedEnumerable<T>(
				shiftIndex ? _head.Concat(e) : _head,
				shiftIndex ? _tail : _tail.Concat(e),
				shiftIndex ? (SpliceIndex + len) : SpliceIndex,
				Count + len);
		}

		public IEnumerable<T> InsertSegment(IEnumerable<T> e)
			=> _head == Empty
			? (_tail == Empty ? e : e.Concat(_tail))
			: (_tail == Empty ? _head.Concat(e) : _head.Concat(e).Concat(_tail));

		public IEnumerable<T> Insert(T e, int repeat = 1)
			=> InsertSegment(Enumerable.Repeat(e, repeat));

		public IEnumerator<T> GetEnumerator()
			=> _head.Concat(_tail).GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();
	}

	public static class SplicedEnumerable
	{
		public static SplicedEnumerable<T> Create<T>(IReadOnlyCollection<T> source, int index)
			=> new(source, index);

		public static SplicedEnumerable<T> Create<T>(IEnumerable<T> source, int index)
			=> new(source.ToImmutableArray(), index);

		public static SplicedEnumerable<T> Create<T>(IReadOnlyCollection<T> head, IReadOnlyCollection<T> tail)
		{
			var index = head.Count;
			return new SplicedEnumerable<T>(head, tail, index, index + tail.Count);
		}

		public static SplicedEnumerable<T> Create<T>(IEnumerable<T> head, IEnumerable<T> tail)
			=> Create(head.ToImmutableArray(), tail.ToImmutableArray());

		public static SplicedEnumerable<T> SpliceAt<T>(this IReadOnlyCollection<T> source, int index)
			=> new(source, index);

	}
}
