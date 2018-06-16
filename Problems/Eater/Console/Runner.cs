using Solve.Experiment.Console;
using Solve.ProcessingSchemes;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Eater
{
	class Runner : RunnerBase<EaterGenome>
	{
		// Keep some known viable genomes for reintroduction.
		public static readonly string[] Seed =
		{
			"^^^^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^>^^^^^^^^>^^^^^^^>^^^^^^^>^^^^^^>^^^^^^>^^^^^>^^^^^>^^^^>^^^^>^^^>^^^>^^>^^>^>^",
			"^^^>^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^>^^^^^^^^>^^^^^^^>^^^^^^^>^^^^^^>^^^^^^>^^^^^>^^^^^>^^^^>^^^^>^^^>^^^>^^>^^>^>^^^^^>^^^^^>^^^^^^^^^>^^^^^^^>^^"
		};

		readonly ushort _minSamples;
		readonly ushort _minConvSamples;
		readonly EaterFactory Factory = new EaterFactory();


		protected Runner(ushort minSamples, ushort minConvSamples = 20) : base()
		{
			_minSamples = minSamples;
			_minConvSamples = minConvSamples;
		}

		public void Init()
		{
			var problem = new EaterProblemFragmented(10);
			var emitter = new EaterConsoleEmitter(problem.Samples, _minSamples);
			//var scheme = new PyramidPipeline<EaterGenome>(factory, 20, 4, 2, 200);
			//var scheme = new KingOfTheHill<EaterGenome>(factory, 300, _minConvSamples, 5);
			var scheme = new ClassicProcessingScheme<EaterGenome>(Factory, (500, 300, 10));
			//var scheme = new KumiteProcessingScheme<EaterGenome>(Factory, 5);
			//var scheme = new SinglePool<EaterGenome>(factory, 200);

			scheme.AddProblem(problem);

			Init(scheme, emitter);

			//{
			//	var seeds = Seed.Select(s => new EaterGenome(s)).ToArray();//.Concat(Seed.SelectMany(s => factory.Expand(new EaterGenome(s)))).ToArray();
			//	for (var i = 0; i < Seed.Length; i++)
			//	{
			//		var genome = seeds[i];
			//		var result = problem.Samples.TestAll(genome);

			//		if (i == 0)
			//		{
			//			Console.WriteLine("Total possibilities: {0:n0}", result[1].Count);
			//			Console.WriteLine();
			//			Console.WriteLine("Seeded solutions............................");
			//			Console.WriteLine();
			//		}

			//		emitter.EmitTopGenomeFullStats(problem, genome);
			//		Console.WriteLine();
			//	}

			//	if (Seed.Length != 0)
			//	{
			//		Console.WriteLine("............................................");
			//		Console.WriteLine();
			//		Console.WriteLine();
			//	}
			//}
		}

#if DEBUG
		protected override void EmitStats(Cursor cursor)
		{
			base.EmitStats(cursor);

			var metrics = Factory.Metrics;
			var snapshot = metrics.Snapshot.Get();
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
		}
#endif

		static Task Main(string[] args)
		{
			var runner = new Runner(10);
			runner.Init();
			var message = String.Format(
				"Solving Eater Problem... (miniumum {0:n0} samples before displaying)",
				runner._minSamples);
			return runner.Start(message);
		}
	}
}
