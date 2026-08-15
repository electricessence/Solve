using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Solve.Metrics;

/// <summary>
/// A single named, incrementable/decrementable counter handed out by <see cref="CounterRegistry"/>.
/// </summary>
public interface ICounter
{
	void Increment();
	void Increment(long count);
	void Decrement();
	void Decrement(long count);
}

/// <summary>
/// A point-in-time snapshot of every counter a <see cref="CounterRegistry"/> has ever created,
/// keyed by (context, name) -- the same query shape the old App.Metrics-based
/// <c>IProvideMetricValues</c> / <c>MetricsContextValueSource</c> pair provided.
/// </summary>
public interface IMetricsSnapshot
{
	DateTime Timestamp { get; }

	long GetValue(string context, string name);

	IEnumerable<(string Context, string Name, long Value)> Counters { get; }
}

/// <summary>
/// Replaces App.Metrics' <c>IMetricsRoot</c> / <c>IProvideCounterMetrics</c>. Owns a private
/// <see cref="Meter"/>, creates named <see cref="UpDownCounter{T}"/> instruments on demand, and
/// maintains a queryable current-value snapshot via a <see cref="MeterListener"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="System.Diagnostics.Metrics"/> is an emit-only API: <see cref="UpDownCounter{T}.Add(long)"/>
/// pushes a measurement, but there is no built-in way to ask "what is this counter's value right
/// now?". This class rebuilds that pull/snapshot capability the codebase relies on (see
/// <see cref="GenomeFactoryMetrics"/>) by subscribing a <see cref="MeterListener"/> to its own
/// <see cref="Meter"/> at construction time -- before any counters are created -- and aggregating
/// every measurement into a running total per instrument name. Aggregation uses
/// <see cref="Interlocked.Add(ref long, long)"/> against a per-instrument state cell rather than a
/// dictionary write per measurement, so the hot increment/decrement path (millions of calls during
/// a long factory run) stays allocation-free after the counter is first created.
/// </para>
/// <para>
/// Every named counter is backed by an <see cref="UpDownCounter{T}"/> rather than a plain
/// (increment-only) <see cref="Counter{T}"/>: several of the counters this codebase tracks (queue
/// depths, breeding stock) go both up and down, and <see cref="CounterCollection"/> hands out a
/// single uniform <see cref="ICounter"/> shape regardless of whether a given caller ever calls
/// <see cref="ICounter.Decrement()"/>. Using one instrument kind uniformly keeps that abstraction
/// simple; the <see cref="MeterListener"/> aggregator observes both instrument kinds identically,
/// so nothing downstream needs to care which kind actually backs a given counter.
/// </para>
/// </remarks>
public sealed class CounterRegistry : IDisposable
{
	private const char KeySeparator = '/';

	private readonly Meter _meter;
	private readonly MeterListener _listener;
	private readonly ConcurrentDictionary<string, ICounter> _counters = new();
	private readonly ConcurrentDictionary<string, InstrumentState> _values = new();
	private int _disposed;

	public CounterRegistry(string? name = null)
	{
		_meter = new Meter(name is null ? $"Solve.Metrics.{Guid.NewGuid():N}" : $"Solve.Metrics.{name}");
		_listener = new MeterListener
		{
			InstrumentPublished = (instrument, listener) =>
			{
				if (!ReferenceEquals(instrument.Meter, _meter)) return;
				_values.GetOrAdd(instrument.Name, static _ => new InstrumentState());
				listener.EnableMeasurementEvents(instrument);
			}
		};
		_listener.SetMeasurementEventCallback<long>(OnMeasurement);
		_listener.Start();
	}

	private void OnMeasurement(Instrument instrument, long measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
	{
		if (_values.TryGetValue(instrument.Name, out InstrumentState? s))
			Interlocked.Add(ref s.Value, measurement);
	}

	private static string CombineKey(string context, string name) => $"{context}{KeySeparator}{name}";

	private static (string Context, string Name) SplitKey(string key)
	{
		int i = key.IndexOf(KeySeparator);
		return i < 0 ? (string.Empty, key) : (key[..i], key[(i + 1)..]);
	}

	/// <summary>
	/// Gets (creating if necessary) the named counter within <paramref name="context"/>. Repeated
	/// calls with the same (context, name) pair return the same instance.
	/// </summary>
	public ICounter GetOrCreateCounter(string context, string name)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(name);
		string key = CombineKey(context, name);
		return _counters.GetOrAdd(key, k => new CounterImpl(_meter.CreateUpDownCounter<long>(k)));
	}

	private long GetValue(string context, string name)
		=> _values.TryGetValue(CombineKey(context, name), out InstrumentState? s)
			? Interlocked.Read(ref s.Value)
			: 0;

	private IEnumerable<(string Context, string Name, long Value)> EnumerateCounters()
	{
		foreach (KeyValuePair<string, InstrumentState> kv in _values)
		{
			(string context, string name) = SplitKey(kv.Key);
			yield return (context, name, Interlocked.Read(ref kv.Value.Value));
		}
	}

	/// <summary>
	/// Captures the current value of every counter this registry has created. Cheap: no counter
	/// values are copied until the returned snapshot's members are actually read.
	/// </summary>
	public IMetricsSnapshot Snapshot() => new SnapshotImpl(this);

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
		_listener.Dispose();
		_meter.Dispose();
	}

	private sealed class InstrumentState
	{
		public long Value;
	}

	private sealed class CounterImpl(UpDownCounter<long> counter) : ICounter
	{
		public void Increment() => counter.Add(1);
		public void Increment(long count) => counter.Add(count);
		public void Decrement() => counter.Add(-1);
		public void Decrement(long count) => counter.Add(-count);
	}

	private sealed class SnapshotImpl(CounterRegistry registry) : IMetricsSnapshot
	{
		public DateTime Timestamp { get; } = DateTime.Now;
		public long GetValue(string context, string name) => registry.GetValue(context, name);
		public IEnumerable<(string Context, string Name, long Value)> Counters => registry.EnumerateCounters();
	}
}
