using Solve;
using System.Collections.Immutable;

namespace Solve.Tests;

public class FitnessTests
{
	private static readonly ImmutableArray<Metric> ConvergenceMetrics
		= [new Metric(0, "Score", "Score {0:p}", 1, double.Epsilon)];

	private static readonly ImmutableArray<Metric> PlainMetrics
		= [new Metric(0, "Score", "Score {0:p}")];

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
}
