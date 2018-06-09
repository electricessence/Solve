using App.Metrics;
using App.Metrics.Counter;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Solve
{
	public class MetricCollection
	{
		readonly IMetricsRoot Metrics;
		readonly ISet<string> Ignored;

		public MetricCollection(IMetricsRoot metrics)
			: this(metrics, new HashSet<string>())
		{

		}

		public MetricCollection(IMetricsRoot metrics, ISet<string> ignored)
		{
			Metrics = metrics;
			Ignored = ignored;
		}

		public void Ignore(params string[] ignore)
		{
			foreach (var i in ignore) Ignored.Add(i);
		}

		readonly ConcurrentDictionary<string, CounterOptions> Counters = new ConcurrentDictionary<string, CounterOptions>();

		public void Increment(string counterName)
		{
			if (!Ignored.Contains(counterName))
				Metrics.Measure.Counter.Increment(Counters.GetOrAdd(counterName, key => new CounterOptions { Name = key }));
		}

		public void Decrement(string counterName)
		{
			if (!Ignored.Contains(counterName))
				Metrics.Measure.Counter.Decrement(Counters.GetOrAdd(counterName, key => new CounterOptions { Name = key }));
		}

	}
}
