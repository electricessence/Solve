using Open.Disposable;
using System.Runtime.CompilerServices;

namespace Solve;

public class InterlockedInt : IEquatable<InterlockedInt>, IEquatable<int>, IRecyclable, IComparable<InterlockedInt>, IComparable<int>
{
	private int _value;
	public int Value
	{
		get => _value;
		set => Interlocked.Exchange(ref _value, value);
	}

	public int Increment()
		=> Interlocked.Increment(ref _value);

	public int Decrement()
		=> Interlocked.Decrement(ref _value);

	public void Recycle() => _value = 0;

	private static readonly OptimisticArrayObjectPool<InterlockedInt> Pool
		= OptimisticArrayObjectPool.CreateAutoRecycle<InterlockedInt>();

	public static InterlockedInt Init(int value = 0)
	{
		InterlockedInt n = Pool.Take();
		n.Value = value;
		return n;
	}

	public static void Recycle(InterlockedInt n)
		=> Pool.Give(n);

	public bool Equals(InterlockedInt? other)
		=> other is not null && _value == other._value;

	public bool Equals(int value)
		=> _value == value;

	public override bool Equals(object? other)
		=> other is int i
			? Equals(i)
			: other is InterlockedInt ii && Equals(ii);

	public override string ToString() => _value.ToString();

	public override int GetHashCode() => _value.GetHashCode();

#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
	public int CompareTo(InterlockedInt other)
#pragma warning restore CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
	{
		ArgumentNullException.ThrowIfNull(other);
		if (_value > other._value) return 1;
		if (_value < other._value) return -1;
		// Equal.
		return 0;
	}

	public int CompareTo(int other)
	{
		if (_value > other) return 1;
		if (_value < other) return -1;
		// Equal.
		return 0;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int ToInt32() => _value;

	public static InterlockedInt FromInt32(int value) => Init(value);

	public static implicit operator int(InterlockedInt i) => i.Value;

	public static implicit operator InterlockedInt(int value) => Init(value);

	public static bool operator ==(InterlockedInt? a, InterlockedInt? b)
		=> a is null ? b is null : a.Equals(b);
	public static bool operator !=(InterlockedInt? a, InterlockedInt? b)
		=> a is null ? b is not null : !a.Equals(b);

	public static bool operator ==(int a, InterlockedInt b) => b.Equals(a);
	public static bool operator !=(int a, InterlockedInt b) => !b.Equals(a);

	public static bool operator ==(InterlockedInt a, int b) => a.Equals(b);
	public static bool operator !=(InterlockedInt a, int b) => !a.Equals(b);

	public static bool operator <(InterlockedInt a, InterlockedInt b) => a.Value < b._value;
	public static bool operator >(InterlockedInt a, InterlockedInt b) => a.Value > b._value;

	public static bool operator <(int a, InterlockedInt b) => a < b._value;
	public static bool operator >(int a, InterlockedInt b) => a > b._value;

	public static bool operator <(InterlockedInt a, int b) => a._value < b;
	public static bool operator >(InterlockedInt a, int b) => a._value > b;

	public static bool operator <=(InterlockedInt a, InterlockedInt b) => a.Value <= b._value;
	public static bool operator >=(InterlockedInt a, InterlockedInt b) => a.Value >= b._value;

	public static bool operator <=(int a, InterlockedInt b) => a <= b._value;
	public static bool operator >=(int a, InterlockedInt b) => a >= b._value;

	public static bool operator <=(InterlockedInt a, int b) => a._value <= b;
	public static bool operator >=(InterlockedInt a, int b) => a._value >= b;
}
