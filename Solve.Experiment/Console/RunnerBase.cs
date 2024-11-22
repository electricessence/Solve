using App.Metrics;
using Open.DateTimeExtensions;
using Open.Disposable;
using Open.Threading.Tasks;
using System.Diagnostics;
using SystemConsole = System.Console;

namespace Solve.Experiment.Console;

public abstract class RunnerBase<TGenome> : DisposableBase
	where TGenome : class, IGenome
{
	private IMetricsRoot Metrics;

	// ReSharper disable once StaticMemberInGenericType
	private static readonly TimeSpan StatusDelay = TimeSpan.FromSeconds(5);

	// ReSharper disable once NotAccessedField.Local
	private readonly ushort _minConvergenceSamples;
	private readonly Stopwatch _stopwatch;
	private EnvironmentBase<TGenome> Environment;
	private ConsoleEmitterBase<TGenome> Emitter;
	private CursorRange? _lastConsoleStats;

	protected RunnerBase(ushort minConvergenceSamples = 20)
	{
		_minConvergenceSamples = minConvergenceSamples;
		_stopwatch = new Stopwatch();
		_statusEmitter = new ActionRunner(EmitStatsAction);
	}

	// ReSharper disable once VirtualMemberNeverOverridden.Global
	// ReSharper disable once MemberCanBeProtected.Global
	public virtual void Init(
		EnvironmentBase<TGenome> environment,
		ConsoleEmitterBase<TGenome> emitter,
		IMetricsRoot metrics)
	{
		if (Environment is not null)
			throw new InvalidOperationException("Already initialized.");

		Environment = environment ?? throw new ArgumentNullException(nameof(environment));
		Emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
		Metrics = metrics;
		OnInit();
	}

	private void EmitStatsAction() => EmitStatsAction(true);

	private void EmitStatsAction(in bool restartEmitter)
	{
		//_lastEmit = DateTime.Now;
		SynchronizedConsole.OverwriteIfSame(ref _lastConsoleStats, EmitStats);
		if (restartEmitter)
			_statusEmitter?.Defer(StatusDelay);
		else
			_statusEmitter?.Cancel();
	}

	public void Cancel()
		=> Environment.Cancel();

	private readonly ActionRunner _statusEmitter;

	//DateTime _lastEmit = DateTime.MinValue;

	//protected void OnAnnouncement((IProblem<TGenome> Problem, IGenomeFitness<TGenome> GenomeFitness) announcement)
	//{
	//	Emitter.EmitTopGenomeStats(announcement);
	//	if (DateTime.Now - _lastEmit > StatusDelay)
	//	{
	//		EmitStatsAction();
	//	}
	//}

	// ReSharper disable once MemberCanBeProtected.Global
	public async Task Start(string info)
	{
		SystemConsole.ResetColor();
		SystemConsole.Clear();
		if (!string.IsNullOrWhiteSpace(info))
		{
			SystemConsole.WriteLine(info);
			SystemConsole.WriteLine();
		}

		SystemConsole.WriteLine("Starting...");
		SystemConsole.SetCursorPosition(0, SystemConsole.CursorTop - 1);

		Environment
			.Subscribe(o =>
				{
					IProblem<TGenome> problem = o.Problem;
					Emitter.EmitTopGenomeStats(o);

					if (!problem.HasConverged && problem.Pools.All(pool => pool.BestFitness.Fitness?.HasConverged(_minConvergenceSamples) ?? false))
						problem.Converged();

					if (Environment.HaveAllProblemsConverged)
						Environment.Cancel();
				},
				ex => SystemConsole.WriteLine(ex.GetBaseException()),
				() =>
				{
					Environment.Cancel();
					SynchronizedConsole.OverwriteIfSame(ref _lastConsoleStats, EmitStats);
				});

		_stopwatch.Start();
		_ = _statusEmitter.Defer(StatusDelay);

		try
		{
			await Environment.Start().ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{ }

		Environment.Cancel();
		EmitStatsAction(false);
		OnComplete();
		SystemConsole.WriteLine("Done.");
	}

	protected virtual void OnInit()
	{
	}

	public IProvideMetricValues MetricsSnapshot => Metrics.Snapshot;

	protected virtual void EmitStats(Cursor cursor)
	{
		SystemConsole.WriteLine("{0} total time                    ", _stopwatch.Elapsed.ToStringVerbose());
		foreach (IProblem<TGenome> p in Environment.Problems)
		{
			long tc = p.TestCount;
			if (tc != 0)
			{
				SystemConsole.WriteLine("{0}:\t{1:n0} tests, {2:n0} ticks average                        ", p.ID, tc, _stopwatch.ElapsedTicks / tc);
			}
		}

		SystemConsole.WriteLine();

#if DEBUG
		if (Metrics is null) return;
		MetricsDataValueSource snapshot = Metrics.Snapshot.Get();
		Debug.Write(StringBuilderPool.RentToString(sb =>
		{
			sb.AppendLine("\n==============================================================");
			sb.AppendLine("Timestamp:");
			sb.Append(snapshot.Timestamp).AppendLine();
			sb.AppendLine("--------------------------------------------------------------");

			foreach (MetricsContextValueSource? context in snapshot.Contexts)
			{
				foreach (App.Metrics.Counter.CounterValueSource? counter in context.Counters)
				{
					sb.Append(counter.Name).AppendLine(":");
					sb.Append(counter.Value.Count).AppendLine();
					sb.AppendLine("--------------------------------------------------------------");
				}
			}
		}));
#endif
	}

	protected virtual void OnComplete() => _statusEmitter.Dispose();//SystemConsole.WriteLine();//SystemConsole.WriteLine("Press any key to continue.");//SystemConsole.ReadKey();
	protected override void OnDispose() => _statusEmitter.Dispose();
}
