using Eater;
using Solve.Metrics;
using Solve.ProcessingSchemes;
using System.Collections.Immutable;

namespace Solve.Tests;

/// <summary>
/// Regression coverage for 15-0015: a cancel issued while the tower's channel-based
/// pipeline is congested must unblock every pending channel write and let the scheme's
/// <c>Start()</c> task complete promptly, instead of hanging past the cancellation
/// because a <c>WriteAsync</c> (or a reader's wait for the next item) had no
/// cancellation token attached.
/// </summary>
public class TowerCancellationPropagationTests
{
	private static GenomeFactory CreateFactory()
		=> new(new CounterRegistry(), seeds: null, leftTurnDisabled: true);

	private static readonly ImmutableArray<Metric> CongestionMetrics
		= [new Metric(0, "M", "M {0:n2}")];

	/// <summary>
	/// A problem whose first <paramref name="warmupCount"/> evaluations complete
	/// immediately — enough to drive several genomes through level 0's Pool channel,
	/// trigger promotion into deeper levels, and fill those levels' Pool channels too,
	/// exercising <c>Level.ProcessContenderSafelyAsync</c>'s <c>Pool.Writer.WriteAsync</c>
	/// fallback along the way whenever a level's own pool reader is itself parked
	/// promoting a winner onto a busy shared queue (see the comment on
	/// <c>Level.ProcessContenderSafelyAsync</c>'s non-blocking-preferred Pool write for
	/// why that cross-channel stall is possible) — and every evaluation after that
	/// blocks forever. Because each of the tower's fixed evaluation workers permanently
	/// parks on the very first post-warmup item it happens to dequeue, available workers
	/// drain one at a time until none are left, at which point
	/// <c>ProblemTower</c>'s shared <c>_evaluationQueue</c> fills to its bounded capacity
	/// and stays there deterministically — no timing race required to prove the new
	/// shared evaluation-dispatch channel (<c>DispatchAsync</c>'s <c>WriteAsync</c>) gets
	/// congested and stays congested through the moment <c>Cancel()</c> is called.
	/// </summary>
	private sealed class SlowAfterWarmupProblem(int warmupCount) : ProblemBase<Genome>(
		4, 2, (CongestionMetrics, (g, v) => new Fitness(CongestionMetrics, ImmutableArray.Create(v[0]))))
	{
		private int _count;

		// Never actually invoked (ProcessSampleMetricsAsync is overridden below instead),
		// but the base class requires an implementation.
		protected override double[] ProcessSampleMetrics(Genome g, long sampleId) => [0.5];

		protected override async ValueTask<double[]> ProcessSampleMetricsAsync(Genome g, long sampleId)
		{
			if (Interlocked.Increment(ref _count) > warmupCount)
			{
				// Never completes: simulates a permanently stuck/congested evaluation so
				// every evaluation worker that dequeues one of these parks forever,
				// deterministically saturating the shared evaluation queue once enough
				// of them have landed.
				await new TaskCompletionSource<double[]>(TaskCreationOptions.RunContinuationsAsynchronously).Task
					.ConfigureAwait(false);
			}

			return [0.5];
		}
	}

	[Fact]
	public async Task CancelWhileCongestedCompletesStartPromptly()
	{
		// Regression: before this fix, neither the per-level Pool.Writer.WriteAsync
		// calls (TowerScheme.Level.cs, both the promotion path and the TryWrite
		// fallback) nor ProblemTower's shared _evaluationQueue WriteAsync
		// (DispatchAsync, called by both the producer loop and level promotion) accepted
		// a cancellation token, so a Cancel() issued while any of those writes was
		// parked on a full channel never unblocked — Start() would hang indefinitely
		// instead of completing.
		GenomeFactory factory = CreateFactory();
		var scheme = new TowerScheme<Genome>(factory, new SchemeConfig
		{
			MaxLevels = 4,
			PoolSize = (2, 2, 0),
			MaxConcurrentEvaluations = 4,
		});
		var problem = new SlowAfterWarmupProblem(warmupCount: 60);
		scheme.AddProblem(problem);

		Exception? observed = null;
		scheme.Subscribe(_ => { }, ex => observed = ex);

		Task run = scheme.Start();

		// Let the warmup evaluations flow through the pipeline (filling and promoting
		// across several levels) and then let the tower run itself into permanent
		// shared-queue saturation once the warmup budget is exhausted and every
		// evaluation worker has parked on a never-completing evaluation. By this point
		// the producer loop's own PostAsync call is essentially guaranteed to be parked
		// on the congested shared queue too.
		await Task.Delay(TimeSpan.FromSeconds(2));

		scheme.Cancel();

		Task first = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10)));
		Assert.True(ReferenceEquals(first, run),
			"Start() did not complete within 10 seconds of Cancel() while the pipeline was congested " +
			"— a channel write or reader wait is still blocked without observing the cancellation token.");

		try { await run; }
		catch (OperationCanceledException) { }

		// The abort must be a silent, normal shutdown signal — not surfaced as a level
		// fault via ProblemTower.OnLevelFault.
		Assert.Null(observed);
	}
}
