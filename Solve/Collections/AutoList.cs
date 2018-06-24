using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Solve.Collections
{
	public class AutoList<T> : IReadOnlyList<T>
	{
		public AutoList(in int count, Func<int, T> factory)
		{
			if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "Must be a positive value.");
			_factory = factory ?? throw new ArgumentNullException(nameof(factory));
			Contract.EndContractBlock();

			Count = count;
		}

		readonly Func<int, T> _factory;
		readonly ConcurrentDictionary<int, T> _cache = new ConcurrentDictionary<int, T>();

		public T this[int index]
		{
			get
			{
				if (index < 0)
					throw new ArgumentOutOfRangeException(nameof(index), index, "Must be a positive value.");
				if (index >= Count)
					throw new ArgumentOutOfRangeException(nameof(index), index, "Must be less than the size of the collection.");
				return _cache.GetOrAdd(index, _factory);
			}
		}

		public int Count { get; }

		public IEnumerator<T> GetEnumerator()
		{
			var len = Count;
			for (var i = 0; i < len; i++)
				yield return this[i];
		}

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();
	}
}
