using Open.Disposable;
using System.Collections.Concurrent;

namespace Solve;

public class LossTracker : DisposableBase
{
	private ConcurrentDictionary<int, InterlockedInt>? _levelLosses = new();

	public InterlockedInt this[int level] => _levelLosses!.GetOrAdd(level, _ => new InterlockedInt());

	protected int _lastRejectionLevel = -1;
	protected int _consecutiveRejection;
	public int ConsecutiveRejection => _consecutiveRejection;

	protected int _rejectionCount;
	public int RejectionCount => _rejectionCount;
	public virtual int IncrementRejection(int level)
	{
		// A rejection only extends the streak when it occurs at the level immediately
		// following the previous rejection. Any break in that sequence — a skipped
		// level or a repeat at the same level — starts a new streak of one.
		_consecutiveRejection = _lastRejectionLevel == level - 1
			? _consecutiveRejection + 1
			: 1;
		_lastRejectionLevel = level;
		return ++_rejectionCount;
	}

	protected override void OnDispose()
	{
		ConcurrentDictionary<int, InterlockedInt>? losses = Interlocked.Exchange(ref _levelLosses, null);
		if (losses is null) return;

		foreach (InterlockedInt? value in losses.Values) InterlockedInt.Recycle(value);
	}
}
