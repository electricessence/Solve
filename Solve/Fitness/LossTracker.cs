using Open.Disposable;
using System.Collections.Concurrent;
using System.Threading;

namespace Solve;

public class LossTracker : DisposableBase
{
	ConcurrentDictionary<int, InterlockedInt>? _levelLosses = new();

	public InterlockedInt this[int level] => _levelLosses!.GetOrAdd(level, _ => new InterlockedInt());

	protected int _lastRejectionLevel = -1;
	protected int _consecutiveRejection;
	public int ConcecutiveRejection => _consecutiveRejection;

	protected int _rejectionCount;
	public int RejectionCount => _rejectionCount;
	public virtual int IncrementRejection(int level)
	{
		if (_lastRejectionLevel == level - 1) ++_consecutiveRejection;
		_lastRejectionLevel = level;
		return ++_rejectionCount;
	}

	protected override void OnDispose()
	{
		var losses = Interlocked.Exchange(ref _levelLosses, null);
		if (losses is null) return;

		foreach (var value in losses.Values) InterlockedInt.Recycle(value);
	}
}
