using System;
using System.Threading.Tasks;
using Eater;
using Solve.Schemes;
using System.Threading;
using System.Diagnostics;
using Open.Dataflow;
using Open.Threading;
using Open;

namespace EaterConsole
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("Starting...");

			var problem = new Problem();
			var scheme = new PyramidPipeline<EaterGenome>(
				new EaterFactory(),
				50, 5, 3);
			scheme.AddProblem(problem);

			var cancel = new CancellationTokenSource();
			var sw = new Stopwatch();
			Action emitStats = () =>
			{
				var tc = problem.TestCount;
				if (tc != 0)
				{
					Console.WriteLine("{0} tests, {1} total time, {2} ticks average", tc, sw.Elapsed.ToStringVerbose(), sw.ElapsedTicks / tc);
					Console.WriteLine();
				}
			};

			scheme
				.AsObservable()
				.Subscribe(
					Problem.EmitTopGenomeStats,
					ex => Console.WriteLine(ex.GetBaseException()),
					() =>
					{
						cancel.Cancel();
						emitStats();
					});

			Task.Run(async () =>
			{
				while (!cancel.IsCancellationRequested)
				{
					await Task.Delay(5000, cancel.Token);
					emitStats();
				}


			}, cancel.Token);

			sw.Start();
			scheme
				.Start()
				.OnFullfilled(() => Console.WriteLine("Done."))
				.OnFaulted(ex => { throw ex; })
				.Wait();

			cancel.Cancel();

			Console.WriteLine();
			Console.WriteLine("Press any key to continue.");
			Console.ReadKey();
		}
	}
}
