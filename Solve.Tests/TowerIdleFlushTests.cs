using Eater;
using Solve.Metrics;
using Solve.ProcessingSchemes;
using System.Collections.Immutable;

namespace Solve.Tests;

/// <summary>
/// Coverage for 15-0020: <see cref="SchemeConfig.IdleFlushAfter"/>, the opt-in idle window
/// that flushes a <c>Level</c>'s partially-filled pool through selection once it has received
/// no new entry for the configured duration -- instead of waiting forever for the pool to
/// reach exactly <c>PoolSize</c> (the scheme's original, still-default behavior).
/// </summary>
public class TowerIdleFlushTests
{
	private static GenomeFactory CreateFactory()
		=> new(new CounterRegistry(), seeds: null, leftTurnDisabled: true);

	[Fact]
	public void IdleFlushAfterDefaultsToNull()
	{
		var config = new SchemeConfig();
		Assert.Null(config.IdleFlushAfter);
		Assert.Null(config.Immutable.IdleFlushAfter);
	}

	[Fact]
	public void IdleFlushAfterIsConfigurable()
	{
		TimeSpan window = TimeSpan.FromSeconds(5);
		var config = new SchemeConfig { IdleFlushAfter = window };
		Assert.Equal(window, config.IdleFlushAfter);
		Assert.Equal(window, config.Immutable.IdleFlushAfter);
		Assert.Equal(window, config.Clone().IdleFlushAfter);
	}

	private static readonly ImmutableArray<Metric> CapMetrics
		= [new Metric(0, "M", "M {0:n2}")];

	/// <summary>
	/// A problem whose first <paramref name="cap"/> evaluations (across the whole problem,
	/// regardless of which level they're for) succeed, and every evaluation after that throws
	/// a plain (untokened) <see cref="OperationCanceledException"/>. <c>Level.ProcessContenderSafelyAsync</c>
	/// swallows any <see cref="OperationCanceledException"/> unconditionally as a normal
	/// shutdown signal (see its trailing catch block), so a genome whose evaluation lands past
	/// the cap simply vanishes -- it never reaches a level's pool, and no fault is raised.
	/// Since level 0 is always <c>IsTop</c> until something first gets promoted out of it,
	/// every one of the first <paramref name="cap"/> genomes queues into level 0's pool
	/// regardless of whether it "won" its evaluation. This deterministically caps how many
	/// entries level 0's buffer will ever receive at exactly <paramref name="cap"/>, without
	/// racing real evaluation throughput against a short idle window, and without needing to
	/// stop the producer loop (it keeps minting and evaluating genomes indefinitely -- only
	/// inflow into the pool is capped).
	/// </summary>
	private sealed class CapAtNProblem(int cap) : ProblemBase<Genome>(
		4, 2, (CapMetrics, (_, v) => new Fitness(CapMetrics, ImmutableArray.Create(v[0]))))
	{
		private int _count;

		// Never actually invoked (ProcessSampleMetricsAsync is overridden below instead), but
		// the base class requires an implementation.
		protected override double[] ProcessSampleMetrics(Genome g, long sampleId) => [0.5];

		protected override ValueTask<double[]> ProcessSampleMetricsAsync(Genome g, long sampleId)
		{
			if (Interlocked.Increment(ref _count) > cap)
				throw new OperationCanceledException();

			return new ValueTask<double[]>([0.5]);
		}
	}

	/// <summary>
	/// Subscribes to a scheme's level-creation stream and exposes a task that completes the
	/// moment any level at or beyond index 1 is created. Level 1 can only ever come into
	/// existence via <c>Level.PromoteAsync</c> (called from selection) or the post-win
	/// fast-track continuation -- and the fast-track path requires a level to already be
	/// non-top, which level 0 never is until something has first been promoted out of it -- so
	/// observing level 1 come into being is a reliable proxy for "level 0 ran selection (full
	/// or partial) at least once and promoted a winner."
	/// </summary>
	private static TaskCompletionSource<bool> ObserveLevelOneCreated(TowerScheme<Genome> scheme)
	{
		var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		scheme.LevelCreated.Subscribe(e =>
		{
			if (e.Level >= 1) tcs.TrySetResult(true);
		});
		return tcs;
	}

	private static async Task RunAndCancelAsync(TowerScheme<Genome> scheme, TimeSpan runFor)
	{
		Exception? observed = null;
		scheme.Subscribe(_ => { }, ex => observed = ex);

		Task run = scheme.Start();
		await Task.Delay(runFor);

		scheme.Cancel();
		try { await run.WaitAsync(TimeSpan.FromSeconds(10)); }
		catch (OperationCanceledException) { }
		catch (TimeoutException) { }

		Assert.Null(observed);
	}

	[Fact]
	public async Task PartialPoolDoesNotFlushWhenIdleFlushAfterIsUnset()
	{
		// Regression for the option's hard requirement: with IdleFlushAfter left at its
		// default (null), a level's pool sitting well below PoolSize must never run
		// selection, no matter how long it waits -- the scheme's original, unconditional
		// fixed-fill-only behavior. PoolSize (10) comfortably exceeds the cap (4), and the
		// wait below (300ms) is well beyond the idle window the paired "flush occurs" test
		// uses (150ms) for good measure, even though there is no timer at all here to race
		// against. Kept short deliberately: nothing here bounds how many genomes the
		// producer mints and dispatches (each triggering real generation/registration work
		// plus a thrown-and-caught OperationCanceledException once past the cap) for the
		// full span of the wait, across every evaluation worker concurrently -- a longer
		// window was previously observed to balloon this test's wall time by two orders of
		// magnitude with no gain in the strength of the negative assertion it proves.
		GenomeFactory factory = CreateFactory();
		var scheme = new TowerScheme<Genome>(factory, new SchemeConfig
		{
			MaxLevels = 3,
			PoolSize = (10, 10, 0),
			MaxConcurrentEvaluations = 2,
			// IdleFlushAfter intentionally left unset (null).
		});
		var problem = new CapAtNProblem(cap: 4);
		scheme.AddProblem(problem);

		TaskCompletionSource<bool> levelOneCreated = ObserveLevelOneCreated(scheme);

		await RunAndCancelAsync(scheme, TimeSpan.FromMilliseconds(300));

		Assert.False(levelOneCreated.Task.IsCompleted,
			"Level 1 was created (selection ran) on a partial, below-PoolSize pool even though IdleFlushAfter was never set.");
	}

	[Fact]
	public async Task PartialPoolFlushesAfterIdleWindowWhenSet()
	{
		// With IdleFlushAfter set, a partial cohort (4 entries, below the PoolSize of 10)
		// must run selection once the window elapses with no new arrivals, and promote its
		// winners into the next level -- proven here by level 1 coming into existence, which
		// only happens via a promotion out of level 0's selection.
		GenomeFactory factory = CreateFactory();
		var scheme = new TowerScheme<Genome>(factory, new SchemeConfig
		{
			MaxLevels = 3,
			PoolSize = (10, 10, 0),
			MaxConcurrentEvaluations = 2,
			IdleFlushAfter = TimeSpan.FromMilliseconds(150),
		});
		var problem = new CapAtNProblem(cap: 4);
		scheme.AddProblem(problem);

		TaskCompletionSource<bool> levelOneCreated = ObserveLevelOneCreated(scheme);

		Exception? observed = null;
		scheme.Subscribe(_ => { }, ex => observed = ex);

		Task run = scheme.Start();
		// Exits as soon as level 1 is observed rather than waiting out the full budget, so
		// this test's actual runtime tracks how long the flush genuinely takes (well under a
		// second) rather than a fixed ceiling; the ceiling itself only bounds the failure case.
		Task first = await Task.WhenAny(levelOneCreated.Task, Task.Delay(TimeSpan.FromSeconds(2)));

		scheme.Cancel();
		try { await run.WaitAsync(TimeSpan.FromSeconds(10)); }
		catch (OperationCanceledException) { }
		catch (TimeoutException) { }

		Assert.Null(observed);
		Assert.Same(levelOneCreated.Task, first);
		Assert.True(levelOneCreated.Task.IsCompletedSuccessfully,
			"Level 1 was never created: the partial cohort was not flushed through selection within the idle window.");
	}

	[Fact]
	public async Task SingleEntryCohortIsNotFlushed()
	{
		// A lone entry has no competitor to select against: even with IdleFlushAfter set, a
		// level holding exactly one buffered entry must keep waiting rather than run
		// selection on it (ProcessSelection's midPoint math would otherwise treat that sole
		// entry as a "loser" -- see RunPoolReaderWithIdleFlushAsync's >= 2 guard).
		GenomeFactory factory = CreateFactory();
		var scheme = new TowerScheme<Genome>(factory, new SchemeConfig
		{
			MaxLevels = 3,
			PoolSize = (10, 10, 0),
			MaxConcurrentEvaluations = 2,
			IdleFlushAfter = TimeSpan.FromMilliseconds(150),
		});
		var problem = new CapAtNProblem(cap: 1);
		scheme.AddProblem(problem);

		TaskCompletionSource<bool> levelOneCreated = ObserveLevelOneCreated(scheme);

		// Kept short for the same reason as PartialPoolDoesNotFlushWhenIdleFlushAfterIsUnset
		// above: nothing here bounds runaway producer volume for the span of the wait.
		await RunAndCancelAsync(scheme, TimeSpan.FromMilliseconds(300));

		Assert.False(levelOneCreated.Task.IsCompleted,
			"Level 1 was created (selection ran) on a single-entry cohort, which must wait for at least one competitor.");
	}
}
