using Open.DateTimeExtensions;
using Open.Disposable;
using Solve.Metrics;
using Solve.ProcessingSchemes;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace Solve.Experiment.Console;

/// <summary>
/// Pure, side-effect-free builders for the Spectre.Console renderables <see cref="RunnerBase{TGenome}"/>'s
/// live display and non-interactive fallback both draw from. Kept static and independent of
/// <see cref="EnvironmentBase{TGenome}"/>/<see cref="IGenomeFactory{TGenome}"/> so Solve.Tests can
/// exercise the exact same rendering the production <see cref="RunnerBase{TGenome}.Start"/> loop
/// uses without needing to stand up a real environment (see SpectreRenderingTests).
/// </summary>
public static class RunnerDisplay
{
	public static IRenderable BuildHeader(
		string? info,
		TimeSpan elapsed,
		IReadOnlyList<(int ProblemId, long TestCount, long AvgTicks)> problemStats,
		double noveltySaturation)
	{
		List<IRenderable> lines = [];

		if (!string.IsNullOrWhiteSpace(info))
			lines.Add(new Markup($"[bold]{Markup.Escape(info)}[/]"));

		long totalTests = problemStats.Sum(p => p.TestCount);
		double testsPerSecond = elapsed.TotalSeconds > 0 ? totalTests / elapsed.TotalSeconds : 0d;

		lines.Add(new Text(string.Format(
			"Elapsed: {0}   Total tests: {1:n0}   Tests/sec: {2:n1}   Novelty saturation: {3:P1}",
			elapsed.ToStringVerbose(), totalTests, testsPerSecond, noveltySaturation)));

		foreach ((int problemId, long testCount, long avgTicks) in problemStats)
			lines.Add(new Text($"  Problem {problemId}: {testCount:n0} tests, {avgTicks:n0} ticks average"));

		return new Panel(new Rows(lines)).Header("Status").Expand();
	}

	public static IRenderable BuildStatsTable(IReadOnlyDictionary<string, TopGenomeStat> topGenomeStats)
	{
		var table = new Table().Expand();
		table.AddColumn("Problem.Pool");
		table.AddColumn("Hash");
		table.AddColumn("Genes");
		table.AddColumn("Samples");
		table.AddColumn("Fitness");

		foreach (KeyValuePair<string, TopGenomeStat> kvp in topGenomeStats.OrderBy(kvp => kvp.Key, StringComparer.Ordinal))
		{
			TopGenomeStat s = kvp.Value;
			table.AddRow(
				new Text(kvp.Key),
				new Text(ShortHash(s.GenomeHash)),
				new Text(s.GeneCount.ToString("n0")),
				new Text(s.SampleCount.ToString("n0")),
				new Text(s.FitnessSummary));
		}

		return new Panel(table).Header("Top Genomes").Expand();
	}

	public static IRenderable BuildEvents(IReadOnlyCollection<string> recentEvents)
	{
		IRenderable content = recentEvents.Count == 0
			? new Text("(none yet)")
			: new Rows(recentEvents.Select(l => (IRenderable)new Text(l)));

		return new Panel(content).Header("Recent Events").Expand();
	}

	/// <param name="extraPanels">
	/// Problem-specific renderables from <see cref="ConsoleEmitterBase{TGenome}.BuildExtraPanels"/>
	/// (task 15-0039/15-0038's generic hook) -- e.g. BlackBoxFunction's expression/gauge panels.
	/// Omitted or empty leaves the layout exactly as task 15-0037 built it, so every pre-existing
	/// call site (including the tests in SpectreRenderingTests.cs) keeps working unchanged.
	/// </param>
	public static IRenderable BuildLayout(
		string? info,
		TimeSpan elapsed,
		IReadOnlyList<(int ProblemId, long TestCount, long AvgTicks)> problemStats,
		double noveltySaturation,
		IReadOnlyDictionary<string, TopGenomeStat> topGenomeStats,
		IReadOnlyCollection<string> recentEvents,
		IReadOnlyList<IRenderable>? extraPanels = null)
	{
		List<Layout> rows =
		[
			new Layout("Header").Size(4 + problemStats.Count),
			new Layout("Stats"),
		];

		if (extraPanels is { Count: > 0 })
			rows.Add(new Layout("Extra").Update(new Rows(extraPanels)));

		rows.Add(new Layout("Events").Size(10));

		Layout layout = new Layout("Root").SplitRows([.. rows]);

		layout["Header"].Update(BuildHeader(info, elapsed, problemStats, noveltySaturation));
		layout["Stats"].Update(BuildStatsTable(topGenomeStats));
		layout["Events"].Update(BuildEvents(recentEvents));

		return layout;
	}

	/// <summary>
	/// The redirected-output fallback (task 15-0037 AC5): no cursor control, no live region --
	/// just periodic plain lines appended to whatever <paramref name="console"/> is writing to.
	/// </summary>
	/// <param name="extraPanels">
	/// See <see cref="BuildLayout"/>'s parameter of the same name. Written straight through
	/// <paramref name="console"/> after the plain stats lines -- Spectre's renderables degrade to
	/// plain characters (no cursor control) on a non-interactive/redirected console same as the
	/// rest of this method, so this stays exception-free under task 15-0037 AC5's redirected-run
	/// guarantee. Omitted or empty reproduces the exact pre-15-0039 output.
	/// </param>
	public static void WritePlainStatus(
		IAnsiConsole console,
		string? info,
		TimeSpan elapsed,
		IReadOnlyList<(int ProblemId, long TestCount, long AvgTicks)> problemStats,
		double noveltySaturation,
		IReadOnlyDictionary<string, TopGenomeStat> topGenomeStats,
		IReadOnlyList<IRenderable>? extraPanels = null)
	{
		console.WriteLine($"{elapsed.ToStringVerbose()} total time");

		foreach ((int problemId, long testCount, long avgTicks) in problemStats)
			console.WriteLine($"{problemId}:\t{testCount:n0} tests, {avgTicks:n0} ticks average");

		console.WriteLine($"Novelty saturation: {noveltySaturation:P1}");

		foreach (KeyValuePair<string, TopGenomeStat> kvp in topGenomeStats.OrderBy(kvp => kvp.Key, StringComparer.Ordinal))
		{
			TopGenomeStat s = kvp.Value;
			console.WriteLine($"{kvp.Key}: {ShortHash(s.GenomeHash)} ({s.GeneCount:n0} genes, {s.SampleCount:n0} samples) {s.FitnessSummary}");
		}

		if (extraPanels is { Count: > 0 })
		{
			foreach (IRenderable panel in extraPanels)
				console.Write(panel);
		}

		console.WriteLine();
	}

	private static string ShortHash(string hash)
		=> hash.Length <= 16 ? hash : string.Concat(hash.AsSpan(0, 16), "…");
}

public abstract class RunnerBase<TGenome> : DisposableBase
	where TGenome : class, IGenome
{
	private CounterRegistry? Metrics;

	// ReSharper disable once StaticMemberInGenericType
	private static readonly TimeSpan StatusDelay = TimeSpan.FromSeconds(5);
	// ReSharper disable once StaticMemberInGenericType
	private static readonly TimeSpan LiveRefreshInterval = TimeSpan.FromSeconds(1);

	// ReSharper disable once NotAccessedField.Local
	private readonly ushort _minConvergenceSamples;
	private readonly Stopwatch _stopwatch;
	private readonly IAnsiConsole _console;
	private EnvironmentBase<TGenome>? Environment;
	private ConsoleEmitterBase<TGenome>? Emitter;
	private string? _runInfo;

	private const int MaxRecentEvents = 20;
	private readonly ConcurrentQueue<string> _recentEvents = new();

	// Opt-in stagnation-based termination. Off unless EnableStagnationMonitor is called.
	private TimeSpan? _stagnationWindow;
	private string? _stagnationSummaryPath;
	private StagnationMonitor<TGenome>? _stagnationMonitor;

	/// <param name="minConvergenceSamples">
	/// Minimum sample count a pool's best fitness needs before it can be considered converged.
	/// </param>
	/// <param name="console">
	/// Where the live display/plain-text status renders to. Defaults to <see cref="AnsiConsole.Console"/>
	/// (task 15-0037 AC4) -- tests inject a <c>Spectre.Console.Testing.TestConsole</c> instead.
	/// Kept as the second parameter (after the pre-existing <paramref name="minConvergenceSamples"/>)
	/// so every existing single-argument `: base(x)` call site in the solution keeps binding `x` to
	/// <paramref name="minConvergenceSamples"/> unchanged.
	/// </param>
	protected RunnerBase(ushort minConvergenceSamples = 20, IAnsiConsole? console = null)
	{
		_minConvergenceSamples = minConvergenceSamples;
		_console = console ?? AnsiConsole.Console;
		_stopwatch = new Stopwatch();
	}

	// ReSharper disable once VirtualMemberNeverOverridden.Global
	// ReSharper disable once MemberCanBeProtected.Global
	public virtual void Init(
		EnvironmentBase<TGenome> environment,
		ConsoleEmitterBase<TGenome> emitter,
		CounterRegistry metrics)
	{
		if (Environment is not null)
			throw new InvalidOperationException("Already initialized.");

		Environment = environment ?? throw new ArgumentNullException(nameof(environment));
		Emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
		Metrics = metrics;
		OnInit();
	}

	public void Cancel()
	{
		Debug.Assert(Environment is not null);
		Environment.Cancel();
	}

	/// <summary>
	/// Opts into stagnation-based termination: if <see cref="Start"/> observes no per-pool
	/// fitness improvement for <paramref name="stagnationWindow"/>, the environment is cancelled
	/// automatically and an end-of-run summary artifact is recorded. Off by default — existing
	/// callers are unaffected unless this is called explicitly before <see cref="Start"/>.
	/// </summary>
	/// <param name="stagnationWindow">How long the run may go without improvement before it is cancelled.</param>
	/// <param name="summaryFilePath">
	/// Optional path to write the end-of-run <see cref="StagnationSummary"/> JSON artifact to.
	/// </param>
	public void EnableStagnationMonitor(TimeSpan stagnationWindow, string? summaryFilePath = null)
	{
		_stagnationWindow = stagnationWindow;
		_stagnationSummaryPath = summaryFilePath;
	}

	private void RecordEvent(string line)
	{
		_recentEvents.Enqueue(line);
		while (_recentEvents.Count > MaxRecentEvents && _recentEvents.TryDequeue(out _)) { }
	}

	// ReSharper disable once MemberCanBeProtected.Global
	public async Task Start(string info)
	{
		if (Environment is null) throw new InvalidOperationException("Not initialized. No Environment.");
		if (Emitter is null) throw new InvalidOperationException("Not initialized. No Emitter.");
		if (Metrics is null) throw new InvalidOperationException("Not initialized. No Metrics.");
		Contract.EndContractBlock();

		_runInfo = info;

		// Console.Clear() throws when stdout is redirected (verified failure mode -- task
		// 15-0037 AC5). Spectre's own IAnsiConsole.Clear() has the identical problem: it still
		// probes the real console cursor even when nothing is attached, so it is gated behind
		// the same capability check the rest of the display uses rather than called
		// unconditionally.
		bool interactive = _console.Profile.Capabilities.Interactive;
		if (interactive)
			_console.Clear();

		if (!string.IsNullOrWhiteSpace(info))
		{
			_console.WriteLine(info);
			_console.WriteLine();
		}

		Environment.Subscribe(
			o =>
			{
				IProblem<TGenome> problem = o.Problem;
				Emitter.EmitTopGenomeStats(o);

				if (!problem.HasConverged && problem.Pools.All(pool => pool.BestFitness.Fitness?.HasConverged(_minConvergenceSamples) ?? false))
					problem.Converged();

				if (Environment.HaveAllProblemsConverged)
					Environment.Cancel();
			},
			ex => _console.WriteLine(ex.GetBaseException().ToString()),
			Environment.Cancel);

		Emitter.ChampionAnnounced += RecordEvent;

		// The engine core no longer writes "Level Created" lines to the console itself (see
		// ProblemTower.OnLevelCreated) -- it only raises TowerScheme.LevelCreated. Feed it into
		// the same recent-events log the champion announcements above populate, rendered by the
		// live display's Events panel, instead of racing a cursor-positioned status display the
		// way the pre-Spectre implementation did (task 15-0018). Only TowerScheme currently
		// exposes this observable; other IEnvironment implementations (e.g. DataflowScheme)
		// simply produce no such line, same as before this task.
		IDisposable? levelCreatedSubscription = null;
		if (Environment is TowerScheme<TGenome> towerScheme)
		{
			levelCreatedSubscription = towerScheme.LevelCreated.Subscribe(e =>
				RecordEvent($"Level created: {e.Problem.ID}.{e.Level}"));
		}

		if (_stagnationWindow is { } stagnationWindow)
			_stagnationMonitor = new StagnationMonitor<TGenome>(Environment, stagnationWindow, _stagnationSummaryPath);

		_stopwatch.Start();

		Task displayLoop = interactive
			? RunLiveDisplayAsync(Environment.CancellationToken)
			: RunPlainStatusLoopAsync(Environment.CancellationToken);

		try
		{
			await Environment.Start().ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{ }

		Environment.Cancel();
		_stagnationMonitor?.Dispose();

		try
		{
			await displayLoop.ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{ }

		Emitter.ChampionAnnounced -= RecordEvent;
		levelCreatedSubscription?.Dispose();

		// The periodic loops above only fire every LiveRefreshInterval/StatusDelay; make sure
		// the truly final state (including anything that changed after the last tick) is always
		// shown, matching the pre-Spectre implementation's unconditional post-completion emit.
		if (interactive)
			_console.Write(BuildRenderable());
		else
			WritePlainStatus();

		OnComplete();
		_console.WriteLine("Done.");
	}

	private async Task RunLiveDisplayAsync(CancellationToken token)
	{
		try
		{
			await _console.Live(BuildRenderable()).StartAsync(async ctx =>
			{
				while (!token.IsCancellationRequested)
				{
					try
					{
						await Task.Delay(LiveRefreshInterval, token).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						break;
					}

					DumpDebugMetrics();
					ctx.UpdateTarget(BuildRenderable());
					ctx.Refresh();
				}
			}).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{ }
	}

	private async Task RunPlainStatusLoopAsync(CancellationToken token)
	{
		while (!token.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(StatusDelay, token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				break;
			}

			WritePlainStatus();
		}
	}

	protected virtual void OnInit()
	{
	}

	public IMetricsSnapshot MetricsSnapshot
	{
		get
		{
			Debug.Assert(Metrics is not null);
			return Metrics.Snapshot();
		}
	}

	// 15-0016: novelty-saturation visibility. A clean public property -- rather than inlining
	// the computation inside the status rendering below -- so the Spectre.Console live display
	// (task 15-0037) can bind a widget/column directly to this value without re-deriving it.
	// Reads the same shared CounterRegistry snapshot MetricsSnapshot above already exposes; no
	// new counters, and no locks beyond what CounterRegistry.Snapshot() already takes.
	/// <summary>
	/// Cumulative fraction of genome-factory operator attempts (generation, mutation,
	/// crossover) that failed to produce a genuinely novel genome so far in this run -- see
	/// <see cref="GenomeFactoryMetrics.NoveltySaturation"/> for the ratio definition. A rising
	/// value is an early sign the search has gone sterile, well before champion progress
	/// visibly stalls.
	/// </summary>
	public double NoveltySaturation
		=> Metrics is null ? 0d : GenomeFactoryMetrics.Get(Metrics.Snapshot()).NoveltySaturation;

	private (int ProblemId, long TestCount, long AvgTicks)[] CurrentProblemStats()
	{
		Debug.Assert(Environment is not null);
		return Environment.Problems
			.Select(p => (p.ID, TestCount: p.TestCount, AvgTicks: p.TestCount == 0 ? 0L : _stopwatch.ElapsedTicks / p.TestCount))
			.Where(t => t.TestCount != 0)
			.ToArray();
	}

	private IRenderable BuildRenderable()
	{
		Debug.Assert(Emitter is not null);
		return RunnerDisplay.BuildLayout(
			_runInfo, _stopwatch.Elapsed, CurrentProblemStats(), NoveltySaturation, Emitter.TopGenomeStats, _recentEvents.ToArray(), Emitter.BuildExtraPanels());
	}

	private void WritePlainStatus()
	{
		Debug.Assert(Emitter is not null);
		DumpDebugMetrics();
		RunnerDisplay.WritePlainStatus(
			_console, _runInfo, _stopwatch.Elapsed, CurrentProblemStats(), NoveltySaturation, Emitter.TopGenomeStats, Emitter.BuildExtraPanels());
	}

	[Conditional("DEBUG")]
	private void DumpDebugMetrics()
	{
		if (Metrics is null) return;
		IMetricsSnapshot snapshot = Metrics.Snapshot();
		Debug.Write(StringBuilderPool.RentToString(sb =>
		{
			sb.AppendLine("\n==============================================================");
			sb.AppendLine("Timestamp:");
			sb.Append(snapshot.Timestamp).AppendLine();
			sb.AppendLine("--------------------------------------------------------------");

			foreach ((string context, string name, long value) in snapshot.Counters)
			{
				sb.Append(context).Append('.').Append(name).AppendLine(":");
				sb.Append(value).AppendLine();
				sb.AppendLine("--------------------------------------------------------------");
			}
		}));
	}

	protected virtual void OnComplete() { }

	protected override void OnDispose()
		=> _stagnationMonitor?.Dispose();
}
