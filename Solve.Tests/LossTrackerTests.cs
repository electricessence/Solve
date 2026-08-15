using Solve;

namespace Solve.Tests;

public class LossTrackerTests
{
	[Fact]
	public void IncrementRejection_ConsecutiveLevels_GrowsStreak()
	{
		using var tracker = new LossTracker();

		tracker.IncrementRejection(0);
		Assert.Equal(1, tracker.ConsecutiveRejection);

		tracker.IncrementRejection(1);
		Assert.Equal(2, tracker.ConsecutiveRejection);

		tracker.IncrementRejection(2);
		Assert.Equal(3, tracker.ConsecutiveRejection);
	}

	[Fact]
	public void IncrementRejection_BrokenStreak_ResetsToOne()
	{
		using var tracker = new LossTracker();

		tracker.IncrementRejection(0);
		tracker.IncrementRejection(1);
		Assert.Equal(2, tracker.ConsecutiveRejection);

		// Rejection at level 5 does not immediately follow level 1: the streak resets.
		tracker.IncrementRejection(5);
		Assert.Equal(1, tracker.ConsecutiveRejection);
	}

	[Fact]
	public void IncrementRejection_SameLevelRepeat_DoesNotGrowStreak()
	{
		using var tracker = new LossTracker();

		tracker.IncrementRejection(0);
		tracker.IncrementRejection(1);
		Assert.Equal(2, tracker.ConsecutiveRejection);

		// A repeat rejection at the same level (not the level immediately following)
		// must not grow the consecutive count.
		tracker.IncrementRejection(1);
		Assert.Equal(1, tracker.ConsecutiveRejection);
	}

	[Fact]
	public void IncrementRejection_AlwaysIncrementsTotalRejectionCount()
	{
		using var tracker = new LossTracker();

		Assert.Equal(1, tracker.IncrementRejection(0));
		Assert.Equal(2, tracker.IncrementRejection(1));
		Assert.Equal(3, tracker.IncrementRejection(5)); // Streak reset, total count still grows.
		Assert.Equal(4, tracker.IncrementRejection(5)); // Same-level repeat, total count still grows.
		Assert.Equal(4, tracker.RejectionCount);
	}
}
