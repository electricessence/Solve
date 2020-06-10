using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Solve
{
	public class LossTracker
	{
		readonly ConcurrentDictionary<int, InterlockedInt> _levelLosses = new ConcurrentDictionary<int, InterlockedInt>();

		public InterlockedInt this[int level] => _levelLosses.GetOrAdd(level, _=> new InterlockedInt());

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
	}
}
