using Open.Disposable;
using System.Threading;

namespace Solve
{
	public sealed class InterlockedInt : IRecyclable
	{
		int _value = 0;
		public int Value
		{
			get => _value;
			set => _value = value;
		}

		public InterlockedInt()
		{
		}

		public int Increment()
			=> Interlocked.Increment(ref _value);

		public int Decrement()
			=> Interlocked.Decrement(ref _value);

		public void Add(in int other)
		{
			if (other != 0)
			{
				if (other > 0)
				{
					for (var i = 0; i < other; i++)
						Increment();
				}
				else
				{
					for (var i = 0; i > other; i--)
						Decrement();
				}
			}
		}

		public void Recycle()
		{
			_value = 0;
		}

		static readonly OptimisticArrayObjectPool<InterlockedInt> Pool
			= OptimisticArrayObjectPool.CreateAutoRecycle<InterlockedInt>();

		public static InterlockedInt Init(int value = 0)
		{
			var n = Pool.Take();
			n.Value = value;
			return n;
		}

		public static void Recycle(InterlockedInt n)
			=> Pool.Give(n);
	}
}
