using App.Metrics;
using Open.Threading.Tasks;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SystemConsole = System.Console;

namespace Solve.Experiment.Console
{
	public abstract class RunnerBase<TGenome>
		where TGenome : class, IGenome

	{
		IMetricsRoot Metrics;
		readonly static TimeSpan StatusDelay = TimeSpan.FromSeconds(5);

		readonly ushort _minConvergenceSamples;
		readonly Stopwatch _stopwatch;
		EnvironmentBase<TGenome> Environment;
		ConsoleEmitterBase<TGenome> Emitter;
		CursorRange _lastConsoleStats = null;

		protected RunnerBase(ushort minConvergenceSamples = 20)
		{
			_minConvergenceSamples = minConvergenceSamples;
			_stopwatch = new Stopwatch();
			_statusEmitter = new ActionRunner(EmitStatsAction);
		}

		public virtual void Init(
			EnvironmentBase<TGenome> environment,
			ConsoleEmitterBase<TGenome> emitter,
			IMetricsRoot metrics)
		{
			if (Environment != null)
				throw new InvalidOperationException("Already initialized.");

			Environment = environment;
			Emitter = emitter;
			Metrics = metrics;
			OnInit();
		}

		void EmitStatsAction() => EmitStatsAction(true);
		void EmitStatsAction(in bool restartEmitter)
		{
			_lastEmit = DateTime.Now;
			SynchronizedConsole.OverwriteIfSame(ref _lastConsoleStats, EmitStats);
			if (restartEmitter)
				_statusEmitter.Defer(StatusDelay);
			else
				_statusEmitter.Cancel();
		}

		readonly ActionRunner _statusEmitter;

		DateTime _lastEmit = DateTime.MinValue;

		//protected void OnAnnouncement((IProblem<TGenome> Problem, IGenomeFitness<TGenome> GenomeFitness) announcement)
		//{
		//	Emitter.EmitTopGenomeStats(announcement);
		//	if (DateTime.Now - _lastEmit > StatusDelay)
		//	{
		//		EmitStatsAction();
		//	}
		//}

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

			var cancel = new CancellationTokenSource();

			Environment
				.Subscribe(o => Emitter.EmitTopGenomeStats(o.Problem, o.GenomeFitness.Genome, o.GenomeFitness.Fitness),
					ex => SystemConsole.WriteLine(ex.GetBaseException()),
					() =>
					{
						cancel.Cancel();
						SynchronizedConsole.OverwriteIfSame(ref _lastConsoleStats, EmitStats);
					});

			_stopwatch.Start();
			var c = _statusEmitter.Defer(StatusDelay);

			await Environment.Start();
			cancel.Cancel();
			EmitStatsAction(false);
			SystemConsole.WriteLine("Done.");

			OnComplete();
		}

		protected virtual void OnInit()
		{

		}

		protected virtual void EmitStats(Cursor cursor)
		{
			SystemConsole.WriteLine("{0} total time                    ", _stopwatch.Elapsed.ToStringVerbose());
			foreach (var p in Environment.Problems)
			{
				var tc = p.TestCount;
				if (tc != 0)
				{
					SystemConsole.WriteLine("{0}:\t{1:n0} tests, {2:n0} ticks average                        ", p.ID, tc, _stopwatch.ElapsedTicks / tc);
				}
			}
			SystemConsole.WriteLine();

#if DEBUG
			if (Metrics == null) return;
			var snapshot = Metrics.Snapshot.Get();
			Debug.WriteLine("\n==============================================================");
			Debug.WriteLine($"Timestamp:");
			Debug.WriteLine(snapshot.Timestamp);
			Debug.WriteLine("--------------------------------------------------------------");

			foreach (var context in snapshot.Contexts)
			{
				foreach (var counter in context.Counters)
				{
					Debug.WriteLine($"{counter.Name}:");
					Debug.WriteLine(counter.Value.Count);
					Debug.WriteLine("--------------------------------------------------------------");
				}
			}
#endif
		}


		protected virtual void OnComplete()
		{
			_statusEmitter.Dispose();

			SystemConsole.WriteLine();
			SystemConsole.WriteLine("Press any key to continue.");
			SystemConsole.ReadKey();
		}
	}
}
