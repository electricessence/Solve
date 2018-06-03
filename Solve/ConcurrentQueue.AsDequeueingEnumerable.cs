using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Solve
{
	public static class ConcurrentQueueExtensions
	{
		public static IEnumerable<T> AsDequeueingEnumerable<T>(this ConcurrentQueue<T> source)
		{
			while (source.TryDequeue(out T entry))
				yield return entry;
		}
	}
}
