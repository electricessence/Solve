using System.Threading;

namespace Solve
{
	public struct InterlockedInt
	{
		int _value;
		public int Value => _value;

		public InterlockedInt(int value = 0)
		{
			_value = value;
		}

		public int Increment()
			=> Interlocked.Increment(ref _value);

		public int Decrement()
			=> Interlocked.Decrement(ref _value);

		public int Add(int other)
		{
			int value, sum;

			do
			{
				value = _value;
				sum = value + other;
			}
			while (value != _value || value != Interlocked.CompareExchange(ref _value, sum, value));

			return sum;
		}

		public int RaiseTo(int min)
		{
			int value;
			do
			{
				value = _value;
			}
			while (value < min && value != Interlocked.CompareExchange(ref _value, min, value));

			return value;
		}

		public int LowerTo(int max)
		{
			int value;
			do
			{
				value = _value;
			}
			while (value > max && value != Interlocked.CompareExchange(ref _value, max, value));

			return value;
		}

		public static implicit operator InterlockedInt(int value)
			=> new InterlockedInt(value);
		public static implicit operator int(InterlockedInt value)
			=> value.Value;
	}
}
