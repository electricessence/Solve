/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using Solve.ProcessingSchemes;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace Solve.Telemetry;

#region Event schema
// See RunEventLog.schema.md (colocated) for the human-readable field-by-field description,
// including one example line per event type. The records below are that schema's source of
// truth: [JsonPropertyName] pins each wire field name so a future rename of a C# property does
// not silently change the JSONL contract, and [JsonPolymorphic]/[JsonDerivedType] make the base
// "type" discriminator field appear first in every serialized line without hand-rolling a
// Utf8JsonWriter loop.

/// <summary>
/// Base type for every line written by <see cref="RunEventLog{TGenome}"/>. Serializing any
/// <see cref="RunEvent"/>-typed reference (see <see cref="RunEventLog{TGenome}.Enqueue"/>) emits
/// a single flat JSON object: a <c>"type"</c> discriminator (one of the strings registered via
/// <see cref="JsonDerivedTypeAttribute"/> below) followed by <see cref="ElapsedSeconds"/> and
/// whatever fields the concrete event type adds.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RunStartedEvent), "run_started")]
[JsonDerivedType(typeof(LevelCreatedEvent), "level_created")]
[JsonDerivedType(typeof(ChampionEvent), "champion")]
[JsonDerivedType(typeof(StatusEvent), "status")]
[JsonDerivedType(typeof(FaultEvent), "fault")]
[JsonDerivedType(typeof(RunEndedEvent), "run_ended")]
public abstract record RunEvent
{
	/// <summary>Seconds elapsed since the owning <see cref="RunEventLog{TGenome}"/> was constructed.</summary>
	[JsonPropertyName("elapsed")]
	public required double ElapsedSeconds { get; init; }
}

/// <summary>Written once, synchronously with <see cref="RunEventLog{TGenome}.Attach*"/>, before any other event.</summary>
public sealed record RunStartedEvent : RunEvent
{
	[JsonPropertyName("startedUtc")]
	public required DateTime StartedUtc { get; init; }

	/// <summary>Flattened scheme configuration, or <see langword="null"/> when none was supplied to Attach.</summary>
	[JsonPropertyName("schemeConfig")]
	public SchemeConfigSnapshot? SchemeConfig { get; init; }

	[JsonPropertyName("problemCount")]
	public required int ProblemCount { get; init; }
}

/// <summary>Mirrors a single <c>TowerScheme{TGenome}.LevelCreated</c> notification.</summary>
public sealed record LevelCreatedEvent : RunEvent
{
	[JsonPropertyName("problemId")]
	public required int ProblemId { get; init; }

	[JsonPropertyName("level")]
	public required int Level { get; init; }
}

/// <summary>One entry of a <see cref="ChampionEvent.FitnessAverages"/> array.</summary>
public sealed record FitnessMetricValue(
	[property: JsonPropertyName("metric")] string Metric,
	[property: JsonPropertyName("value")] double Value);

/// <summary>Mirrors a single champion broadcast from the scheme's <c>EnvironmentBase{TGenome}</c>.</summary>
public sealed record ChampionEvent : RunEvent
{
	[JsonPropertyName("problemId")]
	public required int ProblemId { get; init; }

	[JsonPropertyName("poolIndex")]
	public required int PoolIndex { get; init; }

	[JsonPropertyName("genomeHash")]
	public required string GenomeHash { get; init; }

	[JsonPropertyName("geneCount")]
	public required int GeneCount { get; init; }

	[JsonPropertyName("sampleCount")]
	public required int SampleCount { get; init; }

	[JsonPropertyName("fitnessAverages")]
	public required ImmutableArray<FitnessMetricValue> FitnessAverages { get; init; }
}

/// <summary>One entry of a <see cref="StatusEvent.TestCounts"/> array.</summary>
public sealed record ProblemTestCount(
	[property: JsonPropertyName("problemId")] int ProblemId,
	[property: JsonPropertyName("testCount")] long TestCount);

/// <summary>Periodic sample, emitted every sampling interval passed to the constructor.</summary>
public sealed record StatusEvent : RunEvent
{
	[JsonPropertyName("testCounts")]
	public required ImmutableArray<ProblemTestCount> TestCounts { get; init; }

	/// <summary>Total champion events observed so far (this run).</summary>
	[JsonPropertyName("championCount")]
	public required long ChampionCount { get; init; }
}

/// <summary>
/// Written whenever a subscribed source (the champion broadcast or the level-created stream)
/// raises <see cref="IObserver{T}.OnError"/>, or when processing an event throws. Never thrown
/// back to the caller -- see the <c>catch (Exception)</c> blocks in <see cref="RunEventLog{TGenome}"/>.
/// </summary>
public sealed record FaultEvent : RunEvent
{
	[JsonPropertyName("message")]
	public required string Message { get; init; }

	/// <summary>Which subscription faulted, e.g. <c>"championBroadcast"</c> or <c>"levelCreated"</c>.</summary>
	[JsonPropertyName("source")]
	public string? Source { get; init; }
}

/// <summary>
/// Written exactly once per <see cref="RunEventLog{TGenome}"/> instance -- whichever of
/// (champion broadcast completes, champion broadcast faults, the attach cancellation token is
/// cancelled, <see cref="RunEventLog{TGenome}.Dispose"/> runs) happens first wins; see
/// <c>TryWriteRunEnded</c>.
/// </summary>
public sealed record RunEndedEvent : RunEvent
{
	[JsonPropertyName("reason")]
	public required string Reason { get; init; }

	[JsonPropertyName("totalTests")]
	public required long TotalTests { get; init; }
}

#endregion

/// <summary>
/// Opt-in run-event logger: subscribes to a scheme's champion broadcast (and, when available, its
/// level-creation stream) plus its problems' test counts, and appends one JSON object per line to
/// a caller-supplied file -- a machine-readable, host-independent record of what happened during a
/// run, complementary to (not a replacement for) any host-specific CSV/console output. See
/// <c>RunEventLog.schema.md</c> for the field-by-field schema this type is the source of truth for.
/// </summary>
/// <remarks>
/// <para>
/// <b>Non-blocking writes.</b> Every public entry point that can be called from the engine's hot
/// path (<see cref="OnChampion"/> via the champion broadcast, <see cref="OnLevelCreated"/> via the
/// level-created stream) only ever does an in-memory <see cref="Channel{T}.Writer"/>.<see cref="ChannelWriter{T}.TryWrite"/>
/// -- an O(1), non-allocating-beyond-the-event-record, non-blocking call. The actual file I/O
/// (JSON serialization + <see cref="StreamWriter.WriteLine(string?)"/> + flush) happens on a single
/// dedicated background <see cref="Task"/> (<see cref="_drainTask"/>) that drains the channel via
/// <c>ChannelReader{T}.ReadAllAsync</c>. This "channel-drained single writer" shape was chosen over
/// a locked/buffered <see cref="StreamWriter"/> written to directly from callers because: (a) it
/// guarantees callers on the hot path never block on file I/O or a writer lock, even under a slow
/// disk or antivirus-scanned output path; (b) a single reader means the drain loop needs no
/// synchronization of its own around the <see cref="StreamWriter"/>; (c) it composes cleanly with
/// multiple independent event sources (champion broadcast, level-created stream, status timer, all
/// on different threads) without each needing to coordinate a shared lock, mirroring
/// <see cref="Solve.StagnationMonitor{TGenome}"/>'s <c>lock (_sync)</c> pattern but pushed off the
/// hot path entirely instead of merely minimized.
/// </para>
/// <para>
/// <b>Flush on dispose.</b> <see cref="Dispose"/> completes the channel's writer and synchronously
/// waits for the drain task to finish (so every event enqueued before <see cref="Dispose"/> was
/// called is guaranteed to have been written), then flushes and closes the underlying
/// <see cref="StreamWriter"/>. The drain loop also flushes after every line so a concurrent tailer
/// (e.g. the live-dashboard host this component exists to feed -- see epic 15-0032) sees events
/// promptly rather than only once a buffer fills or the process exits.
/// </para>
/// </remarks>
public sealed class RunEventLog<TGenome> : IDisposable
	where TGenome : class, IGenome
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = false, // one JSON object per line -- WriteIndented would break JSONL.
		// Genome hashes commonly contain '<'/'>' (see Eater's Genome.Hash); the default encoder
		// escapes those as </> (HTML-safety, irrelevant to a flat file nobody embeds in
		// a <script> tag), which measurably bloats every champion line. Relaxed escaping is safe
		// here since this output is never rendered as HTML/JS.
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
	};

	private readonly TimeSpan _samplingInterval;
	private readonly TimeProvider _timeProvider;
	private readonly Stopwatch _elapsed = Stopwatch.StartNew();
	private readonly Channel<RunEvent> _channel = Channel.CreateUnbounded<RunEvent>(new UnboundedChannelOptions
	{
		SingleReader = true,
		SingleWriter = false,
	});
	private readonly StreamWriter _writer;
	private readonly Task _drainTask;

	private long _championCount;
	private IReadOnlyList<IProblem<TGenome>>? _problems;
	private ITimer? _statusTimer;
	private IDisposable? _championSubscription;
	private IDisposable? _levelCreatedSubscription;
	private CancellationTokenRegistration _cancellationRegistration;

	private int _attached;
	private int _ended;
	private int _disposed;

	/// <param name="outputPath">
	/// File to append JSONL events to. Any existing file at this path is overwritten. The parent
	/// directory is created if it does not already exist.
	/// </param>
	/// <param name="samplingInterval">
	/// How often a <see cref="StatusEvent"/> is emitted once <c>Attach</c> (either overload) has
	/// been called. Must be greater than <see cref="TimeSpan.Zero"/>.
	/// </param>
	/// <param name="timeProvider">Clock used for the status-sampling timer and event timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
	public RunEventLog(string outputPath, TimeSpan samplingInterval, TimeProvider? timeProvider = null)
	{
		ArgumentException.ThrowIfNullOrEmpty(outputPath);
		if (samplingInterval <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(samplingInterval), samplingInterval, "Must be greater than zero.");

		_samplingInterval = samplingInterval;
		_timeProvider = timeProvider ?? TimeProvider.System;

		string? directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
		if (!string.IsNullOrEmpty(directory))
			Directory.CreateDirectory(directory);

		_writer = new StreamWriter(new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read))
		{
			AutoFlush = false,
		};

		_drainTask = Task.Run(DrainAsync);
	}

	/// <summary>
	/// Convenience attach for production use: observes <paramref name="environment"/>'s champion
	/// broadcast, its <see cref="EnvironmentBase{TGenome}.Problems"/> for status sampling, its
	/// <see cref="EnvironmentBase{TGenome}.CancellationToken"/>, and -- when <paramref name="environment"/>
	/// is a <see cref="TowerScheme{TGenome}"/> -- its <see cref="TowerScheme{TGenome}.LevelCreated"/>
	/// stream and <see cref="TowerSchemeBase{TGenome}.Config"/>. Must be called before
	/// <see cref="EnvironmentBase{TGenome}.Start"/> so no early events are missed (mirrors
	/// <c>RunnerBase{TGenome}.Start</c>'s own <c>is TowerScheme{TGenome}</c> check for the same reason).
	/// </summary>
	public void Attach(EnvironmentBase<TGenome> environment)
	{
		ArgumentNullException.ThrowIfNull(environment);

		IObservable<(IProblem<TGenome> Problem, int Level)>? levelCreated
			= environment is TowerScheme<TGenome> tower ? tower.LevelCreated : null;

		ISchemeConfig? schemeConfig
			= environment is TowerSchemeBase<TGenome> towerBase ? towerBase.Config : null;

		Attach(environment, environment.Problems, levelCreated, schemeConfig, environment.CancellationToken);
	}

	/// <summary>
	/// Testable core: attaches to explicit observables/values instead of a real
	/// <see cref="EnvironmentBase{TGenome}"/>, so a test can drive a fake/synthetic run (e.g. via
	/// <see cref="System.Reactive.Subjects.Subject{T}"/>) without paying for a real scheme.
	/// </summary>
	/// <param name="championBroadcast">The champion stream to observe (typically an <see cref="EnvironmentBase{TGenome}"/> itself).</param>
	/// <param name="problems">Problems to sample <see cref="IProblem{TGenome}.TestCount"/> from on each status tick, and to count for <see cref="RunStartedEvent.ProblemCount"/>.</param>
	/// <param name="levelCreated">Optional level-creation stream; omit when the scheme has none (only <see cref="TowerScheme{TGenome}"/> currently exposes one).</param>
	/// <param name="schemeConfig">Optional scheme configuration to record on <see cref="RunStartedEvent"/>.</param>
	/// <param name="cancellationToken">Observed so a <see cref="RunEndedEvent"/> with reason <c>"Cancelled."</c> is recorded promptly if the run is cancelled before its broadcast completes.</param>
	public void Attach(
		IObservable<(TGenome Genome, Fitness Fitness, IProblem<TGenome> Problem, int PoolIndex)> championBroadcast,
		IReadOnlyList<IProblem<TGenome>> problems,
		IObservable<(IProblem<TGenome> Problem, int Level)>? levelCreated = null,
		ISchemeConfig? schemeConfig = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(championBroadcast);
		ArgumentNullException.ThrowIfNull(problems);
		if (Interlocked.Exchange(ref _attached, 1) != 0)
			throw new InvalidOperationException("Already attached.");

		_problems = problems;

		Enqueue(new RunStartedEvent
		{
			ElapsedSeconds = _elapsed.Elapsed.TotalSeconds,
			StartedUtc = DateTime.UtcNow,
			SchemeConfig = schemeConfig is null ? null : SchemeConfigSnapshot.From(schemeConfig),
			ProblemCount = problems.Count,
		});

		_championSubscription = championBroadcast.Subscribe(
			OnChampion,
			ex =>
			{
				OnFault(ex, "championBroadcast");
				TryWriteRunEnded($"Faulted: {ex.GetBaseException().Message}");
			},
			() => TryWriteRunEnded("Completed."));

		_levelCreatedSubscription = levelCreated?.Subscribe(
			OnLevelCreated,
			ex => OnFault(ex, "levelCreated"));

		_cancellationRegistration = cancellationToken.Register(() => TryWriteRunEnded("Cancelled."));

		_statusTimer = _timeProvider.CreateTimer(_ => WriteStatus(), null, _samplingInterval, _samplingInterval);
	}

	private void OnChampion((TGenome Genome, Fitness Fitness, IProblem<TGenome> Problem, int PoolIndex) e)
	{
#pragma warning disable CA1031 // Do not catch general exception types: must not crash the caller's broadcast (the engine's hot path).
		try
		{
			Interlocked.Increment(ref _championCount);

			ImmutableArray<FitnessMetricValue> averages = e.Fitness.MetricAverages
				.Select(m => new FitnessMetricValue(m.Metric.Name, m.Value))
				.ToImmutableArray();

			Enqueue(new ChampionEvent
			{
				ElapsedSeconds = _elapsed.Elapsed.TotalSeconds,
				ProblemId = e.Problem.ID,
				PoolIndex = e.PoolIndex,
				GenomeHash = e.Genome.Hash,
				GeneCount = e.Genome.GeneCount,
				SampleCount = e.Fitness.SampleCount,
				FitnessAverages = averages,
			});
		}
		catch (Exception ex)
		{
			OnFault(ex, "championBroadcast");
		}
#pragma warning restore CA1031
	}

	private void OnLevelCreated((IProblem<TGenome> Problem, int Level) e)
	{
#pragma warning disable CA1031 // Do not catch general exception types: must not crash the caller's broadcast (the engine's hot path).
		try
		{
			Enqueue(new LevelCreatedEvent
			{
				ElapsedSeconds = _elapsed.Elapsed.TotalSeconds,
				ProblemId = e.Problem.ID,
				Level = e.Level,
			});
		}
		catch (Exception ex)
		{
			OnFault(ex, "levelCreated");
		}
#pragma warning restore CA1031
	}

	private void OnFault(Exception ex, string source)
		=> Enqueue(new FaultEvent
		{
			ElapsedSeconds = _elapsed.Elapsed.TotalSeconds,
			Message = ex.GetBaseException().Message,
			Source = source,
		});

	private void WriteStatus()
	{
		IReadOnlyList<IProblem<TGenome>>? problems = _problems;
		if (problems is null) return;

#pragma warning disable CA1031 // Do not catch general exception types: must not crash the timer thread.
		try
		{
			ImmutableArray<ProblemTestCount> counts = problems
				.Select(p => new ProblemTestCount(p.ID, p.TestCount))
				.ToImmutableArray();

			Enqueue(new StatusEvent
			{
				ElapsedSeconds = _elapsed.Elapsed.TotalSeconds,
				TestCounts = counts,
				ChampionCount = Interlocked.Read(ref _championCount),
			});
		}
		catch (Exception ex)
		{
			OnFault(ex, "status");
		}
#pragma warning restore CA1031
	}

	private void TryWriteRunEnded(string reason)
	{
		if (Interlocked.CompareExchange(ref _ended, 1, 0) != 0) return;

		long totalTests = _problems?.Sum(p => p.TestCount) ?? 0;
		Enqueue(new RunEndedEvent
		{
			ElapsedSeconds = _elapsed.Elapsed.TotalSeconds,
			Reason = reason,
			TotalTests = totalTests,
		});
	}

	private void Enqueue(RunEvent evt)
		=> _channel.Writer.TryWrite(evt);

	private async Task DrainAsync()
	{
		await foreach (RunEvent evt in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
		{
#pragma warning disable CA1031 // Do not catch general exception types: a single bad write must not kill the drain loop or the run it's observing.
			try
			{
				_writer.WriteLine(JsonSerializer.Serialize(evt, JsonOptions));
				_writer.Flush();
			}
			catch (Exception) { /* best-effort: telemetry must never be the reason a run fails. */ }
#pragma warning restore CA1031
		}
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

		_statusTimer?.Dispose();
		_championSubscription?.Dispose();
		_levelCreatedSubscription?.Dispose();
		_cancellationRegistration.Dispose();

		TryWriteRunEnded("Disposed.");

		_channel.Writer.Complete();
#pragma warning disable CA1031 // Do not catch general exception types: Dispose must not throw.
		try { _drainTask.GetAwaiter().GetResult(); }
		catch (Exception) { /* the drain loop already swallows its own per-line failures; this is belt-and-suspenders. */ }
#pragma warning restore CA1031

		_writer.Flush();
		_writer.Dispose();
	}
}
