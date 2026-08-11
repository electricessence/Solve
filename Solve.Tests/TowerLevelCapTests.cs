using App.Metrics;
using Eater;
using Solve.ProcessingSchemes;
using System.Collections.Immutable;

namespace Solve.Tests;

public class TowerLevelCapTests
{
	private static GenomeFactory CreateFactory()
		=> new(new MetricsBuilder().Build().Provider.Counter, seeds: null, leftTurnDisabled: true);

	[Fact]
	public async Task TerminalLevelDoesNotPromoteOrThrow()
	{
		// Regression: with the old code, the tower dammed the moment it reached
		// MaxLevels (a rogue extra level promoted into a throwing constructor and
		// its dead reader blocked every level below it). A tiny tower saturates
		// within seconds, so continued evaluation growth proves the fix.
		var factory = CreateFactory();
		var scheme = new TowerScheme<Genome>(factory, new SchemeConfig
		{
			MaxLevels = 3,
			PoolSize = (4, 4, 0),
		});
		var problem = Eater.Problem.CreateFitnessPrimary(6);
		scheme.AddProblem(problem);

		Exception? observed = null;
		scheme.Subscribe(_ => { }, ex => observed = ex);

		Task run = scheme.Start();

		await Task.Delay(TimeSpan.FromSeconds(6));
		long t1 = problem.TestCount;
		await Task.Delay(TimeSpan.FromSeconds(2));
		long t2 = problem.TestCount;

		scheme.Cancel();
		try { await run.WaitAsync(TimeSpan.FromSeconds(10)); }
		catch (OperationCanceledException) { }
		catch (TimeoutException) { }

		Assert.Null(observed);
		Assert.True(t2 > t1, $"TestCount stalled after saturation: {t1} → {t2} (tower dammed at MaxLevels).");
	}

	private static readonly ImmutableArray<Metric> FaultMetrics
		= [new Metric(0, "M", "M {0:n2}")];

	private sealed class FaultingProblem() : ProblemBase<Genome>(
		4, 2, (FaultMetrics, (g, v) => new Fitness(FaultMetrics, ImmutableArray.Create(v[0]))))
	{
		// sampleId is the tower level index: throwing only at level >= 1 places the
		// first fault inside a level READER's promotion chain (level-0 evaluations
		// run on the producer chain and would fault the wrong path).
		protected override double[] ProcessSampleMetrics(Genome g, long sampleId)
			=> sampleId >= 1
				? throw new InvalidOperationException("Forced level fault for testing.")
				: [0.5];
	}

	[Fact]
	public async Task LevelFaultReachesSchemeObservers()
	{
		// Regression: a faulted level reader must surface to scheme observers.
		// (Previously the scheme's internal champion subscription — observer #0 —
		// used Rx's default rethrowing onError, aborting OnError fan-out before
		// any external observer was reached.)
		var factory = CreateFactory();
		var scheme = new TowerScheme<Genome>(factory, new SchemeConfig
		{
			MaxLevels = 10,
			PoolSize = (4, 4, 0),
		});
		scheme.AddProblem(new FaultingProblem());

		var faulted = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
		scheme.Subscribe(_ => { }, ex => faulted.TrySetResult(ex));

		Task run = scheme.Start();
		Task first = await Task.WhenAny(faulted.Task, Task.Delay(TimeSpan.FromSeconds(10)));
		scheme.Cancel();
		_ = run.ContinueWith(t => _ = t.Exception, TaskScheduler.Default); // observe any producer fault

		Assert.Same(faulted.Task, first);
		Exception received = await faulted.Task;
		Assert.Contains("Forced level fault", received.GetBaseException().Message);
	}
}
