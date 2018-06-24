using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Solve
{
	public interface IAsyncFitnessResult<TIndex, TValue>
	{
		ValueTask<TValue> this[TIndex index] { get; }
	}

	public class AsyncFitnessResults<TSampleID, TIndex, TValue>
	{
		public AsyncFitnessResults(Func<TSampleID, IAsyncFitnessResult<TIndex, TValue>> resultFactory)
		{
			_resultFactory = resultFactory ?? throw new ArgumentNullException(nameof(resultFactory));
			_results = new ConcurrentDictionary<TSampleID, IAsyncFitnessResult<TIndex, TValue>>();
		}
		readonly Func<TSampleID, IAsyncFitnessResult<TIndex, TValue>> _resultFactory;
		readonly ConcurrentDictionary<TSampleID, IAsyncFitnessResult<TIndex, TValue>> _results;

		public IAsyncFitnessResult<TIndex, TValue> this[TSampleID sampleId]
			=> _results.GetOrAdd(sampleId, _resultFactory);
	}

	public class AsyncFitnessResults<TIndex, TValue> : AsyncFitnessResults<long, TIndex, TValue>
	{
		public AsyncFitnessResults(Func<long, IAsyncFitnessResult<TIndex, TValue>> resultFactory)
			: base(resultFactory)
		{

		}
	}

	public class AsyncFitnessResults<TValue> : AsyncFitnessResults<int, TValue>
	{
		public AsyncFitnessResults(Func<long, IAsyncFitnessResult<int, TValue>> resultFactory)
			: base(resultFactory)
		{

		}
	}
}
