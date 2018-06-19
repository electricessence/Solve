using App.Metrics;
using App.Metrics.Counter;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Solve.Metrics
{
	public class CounterCollection
	{
		readonly IMetricsRoot Metrics;
		readonly ISet<string> Ignored;

		public CounterCollection(IMetricsRoot metrics)
			: this(metrics, new HashSet<string>())
		{

		}

		public CounterCollection(IMetricsRoot metrics, ISet<string> ignored)
		{
			Metrics = metrics;
			Ignored = ignored;
		}

		public void Ignore(params string[] ignore)
		{
			foreach (var i in ignore) Ignored.Add(i);
		}

		readonly ConcurrentDictionary<string, CounterOptions> Counters = new ConcurrentDictionary<string, CounterOptions>();

		public void Increment(string counterName, int count = 1)
		{
			if (count > 0 && !Ignored.Contains(counterName))
			{
				var c = Counters.GetOrAdd(counterName, key => new CounterOptions { Name = key });
				for (var i = 0; i < count; i++)
					Metrics.Measure.Counter.Increment(c);
			}
		}

		public void Decrement(string counterName, int count = 1)
		{
			if (count > 0 && !Ignored.Contains(counterName))
			{
				var c = Counters.GetOrAdd(counterName, key => new CounterOptions { Name = key });
				for (var i = 0; i < count; i++)
					Metrics.Measure.Counter.Decrement(c);
			}
		}

	}
}
