using System.Collections.Concurrent;

namespace Solve.Metrics;

public class CounterCollection(CounterRegistry metrics, string context)
{
	private readonly CounterRegistry Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
	private readonly ConcurrentDictionary<string, ICounter> Counters = new();

	public string Context { get; } = context ?? throw new ArgumentNullException(nameof(context));

	private ICounter CreateCounter(string name) => Metrics.GetOrCreateCounter(Context, name);

	public ICounter this[string name] => Counters.GetOrAdd(name, CreateCounter);
}
