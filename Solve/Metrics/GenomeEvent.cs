namespace Solve.Metrics;

public class GenomeEvent(GenomeEvent.EventType type, string? data = null)
{
	private static long _lastId;

	public enum EventType
	{
		Born,
		Scored,
		Promoted,
		Lost,
		Rejected,
		Died
	}

	public DateTime TimeStamp { get; } = DateTime.Now;

	public long Id { get; } = Interlocked.Increment(ref _lastId);

	public EventType Event { get; } = type;

	public string? Data { get; } = data;
}
