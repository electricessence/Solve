using App.Metrics.Counter;
using System;
using System.Collections.Concurrent;

namespace Solve.Metrics;

public class CounterCollection
{
	private readonly IProvideCounterMetrics Metrics;
	private readonly ConcurrentDictionary<string, ICounter> Counters;

	public string Context { get; }

	public CounterCollection(IProvideCounterMetrics metrics, string context)
	{
		Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
		Context = context ?? throw new ArgumentNullException(nameof(context));
		Counters = new ConcurrentDictionary<string, ICounter>();
	}

	ICounter CreateCounter(string name) => Metrics.Instance(new CounterOptions { Name = name, Context = Context });

	public ICounter this[string name] => Counters.GetOrAdd(name, CreateCounter);
}
