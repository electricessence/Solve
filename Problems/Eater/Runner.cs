using Open.Dataflow;
using Open.Threading.Tasks;
using Solve;
using Solve.Schemes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Eater
{
    class Runner
	{
		// Keep some known viable genomes for reintroduction.
		public static readonly string[] Seed = new string[]
		{
			//"^^^^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^>^^^^^^^^>^^^^^^^>^^^^^^^>^^^^^^>^^^^^^>^^^^^>^^^^^>^^^^>^^^^>^^^>^^^>^^>^^>^>^",
			//"^^^>^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^>^^^^^^^^>^^^^^^^>^^^^^^^>^^^^^^>^^^^^^>^^^^^>^^^^^>^^^^>^^^^>^^^>^^^>^^>^^>^>^^^^^>^^^^^>^^^^^^^^^>^^^^^^^>^^"
		};

		static void Main(string[] args)
		{
			uint minSamples = Seed.Length==0 ? 50u : 0u;
			Console.ResetColor();
			Console.Clear();
			Console.WriteLine("Solving Eater Problem... (miniumum {0:n0} samples before displaying)", minSamples);
			Console.WriteLine();

			Console.WriteLine("Starting...");
			Console.SetCursorPosition(0, Console.CursorTop - 1);

			var factory = new EaterFactory();
			var problem = new ProblemFragmented(100);
			var emitter = new ConsoleEmitter(problem.Samples, minSamples);

			var seeds = Seed.Select(s => new EaterGenome(s)).ToArray();//.Concat(Seed.SelectMany(s => factory.Expand(new EaterGenome(s)))).ToArray();
			for (var i = 0; i < Seed.Length; i++)
			{
				var genome = seeds[i];
				var result = problem.Samples.TestAll(genome);

				if (i == 0)
				{
					Console.WriteLine("Total possibilities: {0:n0}", result[1].Count);
					Console.WriteLine();
					Console.WriteLine("Seeded solutions............................");
					Console.WriteLine();
				}

				emitter.EmitTopGenomeFullStats(problem, genome);
				Console.WriteLine();
			}

			if (Seed.Length != 0)
			{
				Console.WriteLine("............................................");
				Console.WriteLine();
				Console.WriteLine();
			}

			var scheme = new PyramidPipeline<EaterGenome>(
				factory,
				20, 4, 2, 200);

			scheme.AddProblem(problem);
			//scheme.AddProblem(new ProblemFullTest());
			scheme.AddSeeds(seeds);

			var cancel = new CancellationTokenSource();
			var sw = new Stopwatch();
			Action<SynchronizedConsole.Cursor> emitStats = cursor =>
			{
				Console.WriteLine("{0} total time                    ", sw.Elapsed.ToStringVerbose());
				foreach (var p in scheme.Problems)
				{
					var tc = ((Problem)p).TestCount;
					if (tc != 0)
					{
						Console.WriteLine("{0}:\t{1:n0} tests, {2:n0} ticks average                        ", p.ID, tc, sw.ElapsedTicks / tc);
					}
				}
				Console.WriteLine();
			};

			SynchronizedConsole.Message lastConsoleStats = null;
			{
				Action<KeyValuePair<IProblem<EaterGenome>, EaterGenome>> onNext;
				if (Seed.Length == 0) onNext = emitter.EmitTopGenomeStats;
				else onNext = emitter.EmitTopGenomeFullStats;

				scheme
					.AsObservable()
					.Subscribe(onNext,
						ex => Console.WriteLine(ex.GetBaseException()),
						() =>
						{
							cancel.Cancel();
							SynchronizedConsole.OverwriteIfSame(ref lastConsoleStats, emitStats);
						});
			}


			Task.Run(async () =>
			{
				while (!cancel.IsCancellationRequested)
				{
					await Task.Delay(5000, cancel.Token);
					SynchronizedConsole.OverwriteIfSame(ref lastConsoleStats, emitStats);
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
