using Open.Threading.Tasks;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SystemConsole = System.Console;

namespace Solve.Experiment.Console
{
	using App.Metrics;

	[SuppressMessage("ReSharper", "VirtualMemberNeverOverridden.Global")]
	public abstract class RunnerBase<TGenome>
		where TGenome : class, IGenome

	{
		IMetricsRoot Metrics;

		// ReSharper disable once StaticMemberInGenericType
		static readonly TimeSpan StatusDelay = TimeSpan.FromSeconds(5);

		// ReSharper disable once NotAccessedField.Local
		readonly ushort _minConvergenceSamples;
		readonly Stopwatch _stopwatch;
		EnvironmentBase<TGenome> Environment;
		ConsoleEmitterBase<TGenome> Emitter;
		CursorRange _lastConsoleStats;

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
			//_lastEmit = DateTime.Now;
			SynchronizedConsole.OverwriteIfSame(ref _lastConsoleStats, EmitStats);
			if (restartEmitter)
				_statusEmitter?.Defer(StatusDelay);
			else
				_statusEmitter?.Cancel();
		}

		readonly ActionRunner _statusEmitter;

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
						var problem = o.Problem;
						var fitnesses = o.GenomeFitness.Fitness;
						var genome = o.GenomeFitness.Genome;
						Emitter.EmitTopGenomeStats(problem, genome, fitnesses);

						if (!problem.HasConverged && fitnesses.All(f => f.HasConverged(_minConvergenceSamples)))
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
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			_statusEmitter.Defer(StatusDelay).ConfigureAwait(false);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

			try
			{
				await Environment.Start();
			}
			catch (OperationCanceledException)
			{ }
			Environment.Cancel();
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
			var sb = new StringBuilder();
			sb.AppendLine("\n==============================================================");
			sb.AppendLine("Timestamp:");
			sb.Append(snapshot.Timestamp).AppendLine();
			sb.AppendLine("--------------------------------------------------------------");

			foreach (var context in snapshot.Contexts)
			{
				foreach (var counter in context.Counters)
				{
					sb.AppendLine($"{counter.Name}:");
					sb.Append(counter.Value.Count).AppendLine();
					sb.AppendLine("--------------------------------------------------------------");
				}
			}

			Debug.Write(sb.ToString());
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
