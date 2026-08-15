using Eater;
using Solve.Metrics;
using Solve.ProcessingSchemes;
using System.Collections.Immutable;

namespace Solve.Tests;

/// <summary>
/// Regression coverage for the tower's shared, bounded evaluation worker stage
/// (10-0003): fitness evaluation must run concurrently across up to
/// <see cref="ISchemeConfig.MaxConcurrentEvaluations"/> workers instead of inline on
/// whichever await chain (producer or a level's promotion path) reached it.
/// </summary>
public class TowerEvaluationWorkerPoolTests
{
	private static GenomeFactory CreateFactory()
		=> new(new CounterRegistry(), seeds: null, leftTurnDisabled: true);

	[Fact]
	public void MaxConcurrentEvaluationsDefaultsToProcessorCount()
	{
		var config = new SchemeConfig();
		Assert.Equal(Environment.ProcessorCount, config.MaxConcurrentEvaluations);
		Assert.Equal(Environment.ProcessorCount, config.Immutable.MaxConcurrentEvaluations);
	}

	[Fact]
	public void MaxConcurrentEvaluationsIsConfigurable()
	{
		var config = new SchemeConfig { MaxConcurrentEvaluations = 3 };
		Assert.Equal(3, config.MaxConcurrentEvaluations);
		Assert.Equal(3, config.Immutable.MaxConcurrentEvaluations);
		Assert.Equal(3, config.Clone().MaxConcurrentEvaluations);
	}

	private static readonly ImmutableArray<Metric> ConcurrencyMetrics
		= [new Metric(0, "M", "M {0:n2}")];

	/// <summary>
	/// A problem whose sample evaluation blocks briefly and tracks how many evaluations
	/// were ever in flight at once, so tests can observe true concurrent execution instead
	/// of inferring it indirectly.
	/// </summary>
	private sealed class ConcurrencyTrackingProblem() : ProblemBase<Genome>(
		4, 2, (ConcurrencyMetrics, (_, v) => new Fitness(ConcurrencyMetrics, ImmutableArray.Create(v[0]))))
	{
		private int _current;
		private int _maxObserved;

		public int MaxObservedConcurrency => Volatile.Read(ref _maxObserved);

		protected override double[] ProcessSampleMetrics(Genome g, long sampleId)
		{
			int current = Interlocked.Increment(ref _current);
			InterlockedMax(ref _maxObserved, current);
			try
			{
				// Long enough that overlapping evaluations are reliably observed, short
				// enough to keep the test fast.
				Thread.Sleep(30);
				return [0.5];
			}
			finally
			{
				Interlocked.Decrement(ref _current);
			}
		}

		private static void InterlockedMax(ref int target, int value)
		{
			int observed;
			do
			{
				observed = Volatile.Read(ref target);
				if (value <= observed) return;
			}
			while (Interlocked.CompareExchange(ref target, value, observed) != observed);
		}
	}

	[Fact]
	public async Task EvaluationsRunConcurrentlyAndRespectTheConfiguredBound()
	{
		// Regression: with evaluation running inline on whichever await chain reached it
		// (the producer, or a level's single pool reader during promotion), only as many
		// evaluations as there were active chains could ever be mid-flight at once. A
		// bounded shared worker stage should let many more run at the same time, up to the
		// configured degree of parallelism, and never past it.
		const int maxConcurrentEvaluations = 4;
		GenomeFactory factory = CreateFactory();
		var scheme = new TowerScheme<Genome>(factory, new SchemeConfig
		{
			MaxLevels = 6,
			PoolSize = (8, 8, 0),
			MaxConcurrentEvaluations = maxConcurrentEvaluations,
		});
		var problem = new ConcurrencyTrackingProblem();
		scheme.AddProblem(problem);

		Exception? observed = null;
		scheme.Subscribe(_ => { }, ex => observed = ex);

		Task run = scheme.Start();
		await Task.Delay(TimeSpan.FromSeconds(3));

		scheme.Cancel();
		try { await run.WaitAsync(TimeSpan.FromSeconds(10)); }
		catch (OperationCanceledException) { }
		catch (TimeoutException) { }

		Assert.Null(observed);

		int maxObserved = problem.MaxObservedConcurrency;
		Assert.True(maxObserved > 1,
			$"Expected overlapping evaluations from a shared worker stage, but max observed concurrency was {maxObserved}.");
		Assert.True(maxObserved <= maxConcurrentEvaluations,
			$"Observed concurrency {maxObserved} exceeded the configured MaxConcurrentEvaluations={maxConcurrentEvaluations}; the bound was not respected.");

		// The producer must have kept submitting while prior genomes evaluated: a
		// meaningfully larger sample count than the configured concurrency bound is only
		// reachable if submission isn't serialized behind each evaluation.
		Assert.True(problem.TestCount > maxConcurrentEvaluations * 4,
			$"TestCount ({problem.TestCount}) suggests evaluation is still serialized with submission.");
	}

	private static readonly ImmutableArray<Metric> FaultMetrics
		= [new Metric(0, "M", "M {0:n2}")];

	private sealed class FaultingAtRootProblem() : ProblemBase<Genome>(
		4, 2, (FaultMetrics, (g, v) => new Fitness(FaultMetrics, ImmutableArray.Create(v[0]))))
	{
		// Unlike TowerLevelCapTests.FaultingProblem (which avoids level 0 because, before
		// this change, level-0 evaluation ran inline on the producer chain with no fault
		// routing), every evaluation now goes through the shared worker stage regardless of
		// level, so a level-0 fault must reach observers too.
		protected override double[] ProcessSampleMetrics(Genome g, long sampleId)
			=> throw new InvalidOperationException("Forced root-level fault for testing.");
	}

	[Fact]
	public async Task RootLevelFaultReachesSchemeObservers()
	{
		GenomeFactory factory = CreateFactory();
		var scheme = new TowerScheme<Genome>(factory, new SchemeConfig
		{
			MaxLevels = 10,
			PoolSize = (4, 4, 0),
		});
		scheme.AddProblem(new FaultingAtRootProblem());

		var faulted = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
		scheme.Subscribe(_ => { }, ex => faulted.TrySetResult(ex));

		Task run = scheme.Start();
		Task first = await Task.WhenAny(faulted.Task, Task.Delay(TimeSpan.FromSeconds(10)));
		scheme.Cancel();
		_ = run.ContinueWith(t => _ = t.Exception, TaskScheduler.Default); // observe any producer fault

		Assert.Same(faulted.Task, first);
		Exception received = await faulted.Task;
		Assert.Contains("Forced root-level fault", received.GetBaseException().Message);
	}
}
