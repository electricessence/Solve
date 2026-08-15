/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using System.Diagnostics;
using System.Text.Json;

namespace Solve;

/// <summary>
/// Per-pool artifact captured when a <see cref="StagnationMonitor{TGenome}"/> terminates a run:
/// the best genome observed for that pool, its fitness metric averages, and how many samples
/// went into that average.
/// </summary>
public sealed record StagnationPoolSummary(
	int PoolIndex,
	string? BestGenomeHash,
	IReadOnlyDictionary<string, double> FitnessAverages,
	int SampleCount);

/// <summary>
/// End-of-run artifact written by <see cref="StagnationMonitor{TGenome}"/> when it terminates a
/// scheme, either because it detected stagnation or because the run was cancelled by other means
/// while the monitor was active.
/// </summary>
public sealed record StagnationSummary(
	DateTime TerminatedUtc,
	string Reason,
	TimeSpan Elapsed,
	IReadOnlyList<StagnationPoolSummary> Pools);

/// <summary>
/// Subscribes to a champion broadcast (typically <see cref="EnvironmentBase{TGenome}"/>) and
/// cancels the run once a configurable window elapses with no per-pool best-fitness improvement.
/// On termination — whether triggered by stagnation or by cancellation from elsewhere — an
/// end-of-run <see cref="StagnationSummary"/> artifact is optionally written to disk.
/// </summary>
/// <remarks>
/// "Improvement" is a lexicographic increase across a pool's fitness metric averages
/// (<see cref="Fitness.MetricAverages"/>); a new champion with an equal average does not reset
/// the stagnation window. This intentionally differs from <see cref="Fitness.CompareTo"/>, which
/// also breaks ties on sample count — sample-count growth alone must not look like improvement
/// here, or a stalled run would never stagnate.
/// </remarks>
public sealed class StagnationMonitor<TGenome> : IDisposable
	where TGenome : class, IGenome
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
	};

	private readonly Action _cancel;
	private readonly TimeSpan _window;
	private readonly string? _summaryFilePath;
	private readonly Stopwatch _elapsed = Stopwatch.StartNew();
	private readonly System.Threading.Lock _sync = new();
	private readonly Dictionary<int, PoolState> _pools = [];
	private readonly IDisposable _subscription;
	private readonly ITimer _timer;
	private readonly CancellationTokenRegistration _cancellationRegistration;

	private int _terminated;
	private int _disposed;

	/// <summary>
	/// The summary produced at termination, or <see langword="null"/> if the monitor has not yet
	/// terminated.
	/// </summary>
	public StagnationSummary? LastSummary { get; private set; }

	/// <summary>
	/// Creates a monitor over any champion broadcast. This overload is the testable core: it
	/// depends only on <see cref="IObservable{T}"/> and a cancel delegate, so a test can drive a
	/// fake sequence (e.g. via <see cref="System.Reactive.Subjects.Subject{T}"/>) without a real
	/// <see cref="EnvironmentBase{TGenome}"/>.
	/// </summary>
	/// <param name="broadcast">The champion stream to observe.</param>
	/// <param name="stagnationWindow">
	/// How long the run may go without a per-pool fitness improvement before <paramref name="cancel"/>
	/// is invoked. Must be greater than <see cref="TimeSpan.Zero"/>.
	/// </param>
	/// <param name="cancel">Invoked once, when stagnation is detected.</param>
	/// <param name="cancellationToken">
	/// Observed so a summary is still recorded if the run is cancelled by other means before
	/// stagnation is detected. A token that can never be cancelled simply never triggers this path.
	/// </param>
	/// <param name="summaryFilePath">
	/// Path to write the end-of-run <see cref="StagnationSummary"/> JSON artifact to. When
	/// <see langword="null"/>, no file is written but <see cref="LastSummary"/> is still populated.
	/// </param>
	/// <param name="timeProvider">Clock used for the stagnation timer; defaults to <see cref="TimeProvider.System"/>.</param>
	public StagnationMonitor(
		IObservable<(TGenome Genome, Fitness Fitness, IProblem<TGenome> Problem, int PoolIndex)> broadcast,
		TimeSpan stagnationWindow,
		Action cancel,
		CancellationToken cancellationToken = default,
		string? summaryFilePath = null,
		TimeProvider? timeProvider = null)
	{
		ArgumentNullException.ThrowIfNull(broadcast);
		ArgumentNullException.ThrowIfNull(cancel);
		if (stagnationWindow <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(stagnationWindow), stagnationWindow, "Must be greater than zero.");

		_cancel = cancel;
		_window = stagnationWindow;
		_summaryFilePath = summaryFilePath;

		TimeProvider clock = timeProvider ?? TimeProvider.System;
		_timer = clock.CreateTimer(_ => OnStagnationTimeout(), null, stagnationWindow, Timeout.InfiniteTimeSpan);
		_subscription = broadcast.Subscribe(OnNext, OnError, OnCompleted);
		_cancellationRegistration = cancellationToken.Register(() => Terminate("Cancelled externally.", invokeCancel: false));
	}

	/// <summary>
	/// Convenience constructor for production use: observes <paramref name="environment"/>'s
	/// champion broadcast directly and cancels it via <see cref="EnvironmentBase{TGenome}.Cancel"/>.
	/// </summary>
	/// <param name="environment">The environment to monitor and, on stagnation, cancel.</param>
	/// <param name="stagnationWindow">How long the run may go without improvement before it is cancelled.</param>
	/// <param name="summaryFilePath">Optional path to write the end-of-run summary artifact to.</param>
	/// <param name="timeProvider">Clock used for the stagnation timer; defaults to <see cref="TimeProvider.System"/>.</param>
	public StagnationMonitor(
		EnvironmentBase<TGenome> environment,
		TimeSpan stagnationWindow,
		string? summaryFilePath = null,
		TimeProvider? timeProvider = null)
		: this(
			  environment ?? throw new ArgumentNullException(nameof(environment)),
			  stagnationWindow,
			  environment.Cancel,
			  environment.CancellationToken,
			  summaryFilePath,
			  timeProvider)
	{
	}

	private sealed class PoolState
	{
		public TGenome? BestGenome;
		public Fitness? BestFitness;
	}

	private void OnNext((TGenome Genome, Fitness Fitness, IProblem<TGenome> Problem, int PoolIndex) e)
	{
		if (Volatile.Read(ref _terminated) != 0) return;

		bool improved;
		lock (_sync)
		{
			if (!_pools.TryGetValue(e.PoolIndex, out PoolState? state))
			{
				state = new PoolState();
				_pools.Add(e.PoolIndex, state);
			}

			improved = IsImprovement(e.Fitness, state.BestFitness);
			if (improved)
			{
				state.BestGenome = e.Genome;
				state.BestFitness = e.Fitness;
			}
		}

		// Reset the countdown outside the lock: the timer has its own synchronization.
		if (improved)
			_timer.Change(_window, Timeout.InfiniteTimeSpan);
	}

	private void OnError(Exception ex)
		=> Terminate($"Faulted: {ex.GetBaseException().Message}", invokeCancel: false);

	private void OnCompleted()
		=> Terminate("Completed.", invokeCancel: false);

	private void OnStagnationTimeout()
		=> Terminate($"Stagnated: no per-pool fitness improvement for {_window}.", invokeCancel: true);

	/// <summary>
	/// Lexicographic comparison of fitness metric averages only. Sample count is intentionally
	/// excluded (unlike <see cref="Fitness.CompareTo"/>): an equal-average champion must not reset
	/// the stagnation window merely because it accumulated more samples.
	/// </summary>
	private static bool IsImprovement(Fitness candidate, Fitness? currentBest)
	{
		if (currentBest is null) return true;

		using IEnumerator<(Metric Metric, double Value)> a = candidate.MetricAverages.GetEnumerator();
		using IEnumerator<(Metric Metric, double Value)> b = currentBest.MetricAverages.GetEnumerator();

		while (a.MoveNext())
		{
			if (!b.MoveNext()) return true;

			double x = a.Current.Value;
			double y = b.Current.Value;
			if (x > y) return true;
			if (x < y) return false;
			// Equal (or both NaN, which compares false both ways) -> fall through to the next metric.
		}

		return false;
	}

	private void Terminate(string reason, bool invokeCancel)
	{
		if (Interlocked.CompareExchange(ref _terminated, 1, 0) != 0) return;

		_timer.Dispose();
		_subscription.Dispose();
		_cancellationRegistration.Dispose();

		if (invokeCancel)
		{
#pragma warning disable CA1031 // Do not catch general exception types: must not crash an unattended run.
			try { _cancel(); }
			catch (Exception) { /* best-effort: the summary below is still recorded regardless. */ }
#pragma warning restore CA1031
		}

		StagnationSummary summary = BuildSummary(reason);
		LastSummary = summary;
		WriteSummaryFile(summary);
	}

	private StagnationSummary BuildSummary(string reason)
	{
		List<StagnationPoolSummary> pools;
		lock (_sync)
		{
			pools = _pools
				.OrderBy(kv => kv.Key)
				.Select(kv =>
				{
					PoolState state = kv.Value;
					Fitness? fitness = state.BestFitness;
					Dictionary<string, double> averages = fitness is null
						? []
						: fitness.MetricAverages.ToDictionary(m => m.Metric.Name, m => m.Value);

					return new StagnationPoolSummary(
						kv.Key,
						state.BestGenome?.Hash,
						averages,
						fitness?.SampleCount ?? 0);
				})
				.ToList();
		}

		return new StagnationSummary(DateTime.UtcNow, reason, _elapsed.Elapsed, pools);
	}

	private void WriteSummaryFile(StagnationSummary summary)
	{
		if (_summaryFilePath is null) return;

#pragma warning disable CA1031 // Do not catch general exception types: the artifact dump must not crash an unattended run.
		try
		{
			string? directory = Path.GetDirectoryName(_summaryFilePath);
			if (!string.IsNullOrEmpty(directory))
				Directory.CreateDirectory(directory);

			File.WriteAllText(_summaryFilePath, JsonSerializer.Serialize(summary, JsonOptions));
		}
		catch (Exception) { /* best-effort artifact dump; LastSummary still holds the result. */ }
#pragma warning restore CA1031
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
		_timer.Dispose();
		_subscription.Dispose();
		_cancellationRegistration.Dispose();
	}
}
