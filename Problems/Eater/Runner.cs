using System;
using System.Threading.Tasks;
using Solve.Schemes;
using System.Threading;
using System.Diagnostics;
using Open.Dataflow;
using Open.Threading;
using Open;
using System.Linq;

namespace Eater
{
	class Runner
	{
		// Keep some known viable genomes for reintroduction.
		public static readonly string[] Seed = new string[]
		{
			//"^^^^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^>^^^^^^^^>^^^^^^^>^^^^^^^>^^^^^^>^^^^^^>^^^^^>^^^^^>^^^^>^^^^>^^^>^^^>^^>^^>^>^",
			//"^^^>^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^>^^^^^^^^>^^^^^^^>^^^^^^^>^^^^^^>^^^^^^>^^^^^>^^^^^>^^^^>^^^^>^^^>^^^>^^>^^>^>^^^^^>^^^^^>^^^^^^^^^<^^^>^^^^^>^^>^^^^^^^^^^^<^^>>^^^^^^>^>^>^>^^>^^^>^^^>^^^^^^>^>^^^^>^^^^^>^^^^^^^^^^^^>^^^^>^<^<^>^^",
			//"^^^>^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^>^^^^^^^^>^^^^^^^>^^^^^^^>^^^^^^>^^^^^^>^^^^^>^^^^^>^^^^>^^^^>^^^>^^^>^^>^^>^>^^^^^>^^^^^>^^^^^^^^^>^^^^^^^>^^"
		};

		static void Main(string[] args)
		{
			Console.WriteLine("Starting...");

			var sc = new SampleCache();

			for (var i = 0; i < Seed.Length; i++)
			{
				var genome = Seed[i];
				var result = sc.TestAll(genome);

				if (i == 0)
				{
					Console.WriteLine("Total possibilities: " + result.Count);
					Console.WriteLine();
				}

				Console.WriteLine("{0}: {1}", i, genome);
				Console.WriteLine("{0} length, {1} average energy", genome.Length, result.Average);
				Console.WriteLine();
			}

			if (Seed.Length != 0)
			{
				Console.WriteLine("Press any key to continue.");
				Console.ReadKey();
				Console.WriteLine();
				Console.WriteLine();
			}


			var problem = new Problem();
			var scheme = new PyramidPipeline<EaterGenome>(
				new EaterFactory(),
				20, 5, 3, 400);

			scheme.AddProblem(problem);
			scheme.AddSeeds(Seed.Select(s => new EaterGenome(s)));

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

			if (Seed.Length == 0)
			{
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
			}
			else
			{
				scheme
					.AsObservable()
					.Subscribe(
						Problem.EmitTopGenomeFullStats,
						ex => Console.WriteLine(ex.GetBaseException()),
						() =>
						{
							cancel.Cancel();
							emitStats();
						});
			}


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
