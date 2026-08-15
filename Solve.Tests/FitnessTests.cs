using Solve;
using System.Collections.Immutable;

namespace Solve.Tests;

public class FitnessTests
{
	// double.Epsilon (smallest subnormal double) is used here deliberately as an
	// effectively-zero tolerance to exercise exact-equality/overshoot semantics of the
	// predicate itself; it is not a realistic production tolerance (see BlackBoxFunction's
	// and Eater's Metric definitions for real-world tolerance choices).
	private static readonly ImmutableArray<Metric> ConvergenceMetrics
		= [new Metric(0, "Score", "Score {0:p}", 1, double.Epsilon)];

	private static readonly ImmutableArray<Metric> PlainMetrics
		= [new Metric(0, "Score", "Score {0:p}")];

	// Mirrors BlackBoxFunction.Problem's Metrics01 Correlation metric (MaxValue 1, Tolerance 1e-7).
	private static readonly ImmutableArray<Metric> BlackBoxCorrelationLikeMetrics
		= [new Metric(0, "Correlation", "Correlation {0:p10}", 1, 1e-7)];

	// Mirrors Eater.Problem's MetricsPrimary Food-Found-Rate metric (MaxValue 1, Tolerance 0).
	private static readonly ImmutableArray<Metric> EaterFoodFoundRateLikeMetrics
		= [new Metric(0, "Food-Found-Rate", "Food-Found-Rate {0:p}", 1, 0)];

	// ProcedureResults stores the SUM over `count` samples; scale so the average equals `value`.
	private static void Merge(Fitness fitness, double value, int count)
		=> fitness.Merge(ImmutableArray.Create(value * count), count);

	[Fact]
	public void OvershootAboveMaxValue_DoesNotThrow_AndConverges()
	{
		var fitness = new Fitness(ConvergenceMetrics);
		Merge(fitness, 1.0 + 1e-9, 10);
		// Previously threw InvalidOperationException("Score has exceeded convergence value").
		Assert.True(fitness.HasConverged(1));
	}

	[Fact]
	public void ExactMaxValue_Converges()
	{
		var fitness = new Fitness(ConvergenceMetrics);
		Merge(fitness, 1.0, 10);
		Assert.True(fitness.HasConverged(1));
	}

	[Fact]
	public void BelowTolerance_DoesNotConverge()
	{
		var fitness = new Fitness(ConvergenceMetrics);
		Merge(fitness, 0.5, 10);
		Assert.False(fitness.HasConverged(1));
	}

	[Fact]
	public void NoConvergenceMetrics_NeverConverges()
	{
		var fitness = new Fitness(PlainMetrics);
		Merge(fitness, 1.0, 10);
		Assert.False(fitness.HasConverged(1));
	}

	[Fact]
	public void InsufficientSamples_DoesNotConverge()
	{
		var fitness = new Fitness(ConvergenceMetrics);
		Merge(fitness, 1.0, 10);
		Assert.False(fitness.HasConverged(100));
	}

	[Fact]
	public void Merge_AccumulatesSampleCount()
	{
		var fitness = new Fitness(ConvergenceMetrics);
		Merge(fitness, 0.5, 3);
		Merge(fitness, 1.0, 4);
		Assert.Equal(7, fitness.SampleCount);
	}

	[Fact]
	public void BlackBoxCorrelation_NearPerfectAverage_Converges()
	{
		// With a realistic tolerance (1e-7) instead of double.Epsilon, a genome averaging
		// 1 - 1e-8 on correlation is within tolerance of the maximum and should converge.
		var fitness = new Fitness(BlackBoxCorrelationLikeMetrics);
		Merge(fitness, 1.0 - 1e-8, 10);
		Assert.True(fitness.HasConverged(1));
	}

	[Fact]
	public void EaterFoodFoundRate_ExactMaxOverSampleMinimum_Converges()
	{
		var fitness = new Fitness(EaterFoodFoundRateLikeMetrics);
		Merge(fitness, 1.0, 40);
		Assert.True(fitness.HasConverged(40));
	}

	[Fact]
	public void EaterFoodFoundRate_NinetyNinePercentAverage_DoesNotConverge()
	{
		var fitness = new Fitness(EaterFoodFoundRateLikeMetrics);
		Merge(fitness, 0.99, 40);
		Assert.False(fitness.HasConverged(40));
	}
}
