using App.Metrics.Counter;
using System.Collections.Concurrent;

namespace Solve.Metrics;

public class CounterCollection(IProvideCounterMetrics metrics, string context)
{
	private readonly IProvideCounterMetrics Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
	private readonly ConcurrentDictionary<string, ICounter> Counters = new();

	public string Context { get; } = context ?? throw new ArgumentNullException(nameof(context));

	private ICounter CreateCounter(string name) => Metrics.Instance(new CounterOptions { Name = name, Context = Context });

	public ICounter this[string name] => Counters.GetOrAdd(name, CreateCounter);
}
