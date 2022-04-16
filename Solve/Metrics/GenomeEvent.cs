using System;
using System.Threading;

namespace Solve.Metrics;

public class GenomeEvent
{
	static long _lastId = 0;

	public enum EventType
	{
		Born,
		Scored,
		Promoted,
		Lost,
		Rejected,
		Died
	}

	public GenomeEvent(EventType type, string? data = null)
	{
		TimeStamp = DateTime.Now;
		Id = Interlocked.Increment(ref _lastId);
		Event = type;
		Data = data;
	}
	public DateTime TimeStamp { get; }

	public long Id { get; }

	public EventType Event { get; }

	public string? Data { get; }
}
