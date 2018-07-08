using Solve.Experiment.Console;
using Solve.ProcessingSchemes;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Eater
{
	class Runner : RunnerBase<Genome>
	{
		// Keep some known viable genomes for reintroduction.
		public static readonly string[] Seed =
		{
			"^^^^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^>^^^^^^^^>^^^^^^^>^^^^^^^>^^^^^^>^^^^^^>^^^^^>^^^^^>^^^^>^^^^>^^^>^^^>^^>^^>^>^",
			"^^^>^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^>^^^^^^^^>^^^^^^^>^^^^^^^>^^^^^^>^^^^^^>^^^^^>^^^^^>^^^^>^^^^>^^^>^^^>^^>^^>^>^^^^^>^^^^^>^^^^^^^^^>^^^^^^^>^^",
			"3^>6^>9^>9^>9^>8^>8^>7^>7^>6^>6^>5^>5^>4^>4^>3^>3^>2^>2^>^>5^>5^>9^>5^"
		};

		readonly ushort _minSamples;
		readonly ushort _minConvSamples;

		protected Runner(ushort minSamples, ushort minConvSamples = 20) : base()
		{
			_minSamples = minSamples;
			_minConvSamples = minConvSamples;
		}

		public void Init()
		{
			ushort size = 10;
			var mathmaticallyCertain = new StringBuilder();
			mathmaticallyCertain.Append(size).Append('^');
			mathmaticallyCertain.Append('>').Append(size - 1).Append('^');
			mathmaticallyCertain.Append('>').Append(size - 1).Append('^');
			//mathmaticallyCertain.Append('>').Append(size - 1).Append('^');
			for (var i = size - 2; i > 0; i--)
				mathmaticallyCertain.Append('>').Append(i).Append('^').Append('>').Append(i).Append('^');
			var seed = new Genome(mathmaticallyCertain.ToString());

			var emitter = new EaterConsoleEmitter(_minSamples);
			emitter.SaveGenomeImage(seed, "LatestSeed");

			var factory = new GenomeFactory(seed, leftTurnDisabled: true);
			var scheme = new TowerProcessingScheme<Genome>(factory, (400, 40, 2));
			scheme.AddProblem(Problem.CreateF0102(size, 40));
			//scheme.AddProblem(EaterProblem.CreateF02(10, 40));

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
