using Open.Collections;
using Open.Threading;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Solve
{
	public class BatchCreator<T>
	{
		public readonly int BatchSize;

		private readonly ConcurrentQueue<T> _values;
		private readonly ConcurrentQueue<T[]> _batches;

		public event EventHandler? BatchReady;

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
			if (value is null) throw new ArgumentNullException(nameof(value));
			_values.Enqueue(value);

			var batched = false;
			while (_values.Count >= BatchSize && ThreadSafety.TryLock(_values, () =>
			{
				batched = false;
				// Use 'if' instead of 'while' to ensure a single 'Add' operation doesn't get trapped in the job of batching.
				if (_values.Count < BatchSize) return;

				_batches.Enqueue(_values.AsDequeueingEnumerable().Take(BatchSize).ToArray());
				batched = true;
			}))
			{
				if (batched)
					BatchReady?.Invoke(this, EventArgs.Empty);
			}
		}

		public bool TryDequeue([NotNullWhen(true)] out T[] batch)
			=> _batches.TryDequeue(out batch!);

	}
}
