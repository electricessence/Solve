using System.Collections;
using System.Collections.Immutable;

namespace Solve;

/// <summary>
/// A sequence split at <see cref="SpliceIndex"/> for insert/remove operations.
/// Invariants: <c>_tail</c> always begins with the element at <see cref="SpliceIndex"/>
/// (when the sequence is non-empty), and enumeration yields exactly <see cref="Count"/> items.
/// Results of <see cref="Remove(int)"/> must be re-spliced (<see cref="SplicedEnumerable.SpliceAt{T}"/>)
/// before further Remove operations: when a Remove empties the tail, the resulting SpliceIndex
/// is clamped into the head and a chained Remove would operate at the wrong position.
/// </summary>
public readonly record struct SplicedEnumerable<T> : IReadOnlyCollection<T>
{
	private readonly IEnumerable<T> _head;
	private readonly IEnumerable<T> _tail;
	public int SpliceIndex { get; }
	public int Count { get; }

	private static readonly IEnumerable<T> Empty = [];

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
			// The element at SpliceIndex must always begin the tail.
			_head = source.Take(SpliceIndex);
			_tail = source.Skip(SpliceIndex);
		}
		else
		{
			_head = Empty;
			_tail = source;
		}
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0046:Convert to conditional expression")]
	public (IEnumerable<T> head, IEnumerable<T> tail) Segments(int remove = 0)
	{
		if (remove == 0) return (_head, _tail);
		int n = SpliceIndex + remove;
		if (n <= 0) return (Empty, _tail);
		if (n >= Count) return (_head, Empty);
		if (remove < 0) return (_head.Take(n), _tail);
		return (_head, _tail.Skip(remove));
	}

	public SplicedEnumerable<T> Remove(int count)
	{
		if (count == 0) return this;

		if (count > 0)
		{
			// Removing forward: skip the removed elements, keeping the remainder of the tail.
			int tailLen = Math.Max(0, Count - SpliceIndex - count);
			IEnumerable<T> tail = tailLen > 0 ? _tail.Skip(count) : Empty;
			return new SplicedEnumerable<T>(_head, tail, SpliceIndex, SpliceIndex + tailLen);
		}

		int headLen = Math.Max(0, SpliceIndex + count);
		IEnumerable<T> head = headLen > 0 ? _head.Take(headLen) : Empty;
		return new SplicedEnumerable<T>(head, _tail, headLen, Count - SpliceIndex + headLen);
	}

	public SplicedEnumerable<T> InsertSegment(IReadOnlyCollection<T> e, bool shiftIndex = false)
	{
		int len = e.Count;
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
		int index = head.Count;
		return new SplicedEnumerable<T>(head, tail, index, index + tail.Count);
	}

	public static SplicedEnumerable<T> Create<T>(IEnumerable<T> head, IEnumerable<T> tail)
		=> Create(head.ToImmutableArray(), tail.ToImmutableArray());

	public static SplicedEnumerable<T> SpliceAt<T>(this IReadOnlyCollection<T> source, int index)
		=> new(source, index);
}
