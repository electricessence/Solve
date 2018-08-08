using Open.Collections;
using Open.Threading;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Solve
{
	public class BatchCreator<T>
	{
		public readonly int BatchSize;

		private readonly ConcurrentQueue<T> _values;
		private readonly ConcurrentQueue<T[]> _batches;


		public BatchCreator(int batchSize)
		{
			if (batchSize < 1)
				throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Must be at least a size of one.");
			Contract.EndContractBlock();

			BatchSize = batchSize;
			_values = new ConcurrentQueue<T>();
			_batches = new ConcurrentQueue<T[]>();
		}

		public void Add(T value)
		{
			_values.Enqueue(value);

			while (_values.Count >= BatchSize && ThreadSafety.TryLock(_values, () =>
			{
				// Use 'if' instead of 'while' to ensure a single 'Add' operation doesn't get trapped in the job of batching.
				if (_values.Count >= BatchSize)
					_batches.Enqueue(_values.AsDequeueingEnumerable().Take(BatchSize).ToArray());
			}))
			{ }
		}

		public bool TryDequeue(out T[] batch)
			=> _batches.TryDequeue(out batch);

	}
}
