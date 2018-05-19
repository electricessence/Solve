using Open.Dataflow;
using Open.Threading.Tasks;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using SystemConsole = System.Console;

namespace Solve.Experiment.Console
{
	public abstract class RunnerBase<TGenome>
		where TGenome : class, IGenome

	{
		readonly Stopwatch _stopwatch;
		EnvironmentBase<TGenome> Environment;
		ConsoleEmitterBase<TGenome> Emitter;
		CursorRange _lastConsoleStats = null;

		protected RunnerBase()
		{
			_stopwatch = new Stopwatch();
		}

		public virtual void Init(
			EnvironmentBase<TGenome> environment,
			ConsoleEmitterBase<TGenome> emitter)
		{
			if (Environment == null)
			{
				Environment = environment;
				Emitter = emitter;
				OnInit();
			}
		}

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
				.AsObservable()
				.Subscribe(Emitter.EmitTopGenomeStats,
					ex => SystemConsole.WriteLine(ex.GetBaseException()),
					() =>
					{
						cancel.Cancel();
						SynchronizedConsole.OverwriteIfSame(ref _lastConsoleStats, EmitStats);
					});

			var status = Task.Run(
				cancellationToken: cancel.Token,
				action: async () =>
				{
					while (!cancel.IsCancellationRequested)
					{
						await Task.Delay(5000, cancel.Token).ContinueWith(t =>
						{
							SynchronizedConsole.OverwriteIfSame(ref _lastConsoleStats, EmitStats);
						});
					}
				});

			_stopwatch.Start();
			await Environment
				.Start()
				.OnFullfilled(() => SystemConsole.WriteLine("Done."))
				.OnFaulted(ex => { throw ex; });

			cancel.Cancel();

			await status;

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
				var tc = ((ProblemBase<TGenome>)p).TestCount;
				if (tc != 0)
				{
					SystemConsole.WriteLine("{0}:\t{1:n0} tests, {2:n0} ticks average                        ", p.ID, tc, _stopwatch.ElapsedTicks / tc);
				}
			}
			SystemConsole.WriteLine();
		}


		protected void OnComplete()
		{
			SystemConsole.WriteLine();
			SystemConsole.WriteLine("Press any key to continue.");
			SystemConsole.ReadKey();
		}
	}
}
