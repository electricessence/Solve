using System;

namespace Solve.Metrics;

public struct SuccessFailCount
{
	private const string MustBeAtLeastZero = "Must be at least zero.";

	public SuccessFailCount(long succeeded, long failed)
	{
		if (succeeded < 0) throw new ArgumentOutOfRangeException(nameof(succeeded), succeeded, MustBeAtLeastZero);
		if (failed < 0) throw new ArgumentOutOfRangeException(nameof(failed), failed, MustBeAtLeastZero);
		Succeeded = succeeded;
		Failed = failed;
	}

	public long Succeeded { get; }
	public long Failed { get; }
}
