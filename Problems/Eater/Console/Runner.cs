﻿using Solve.Experiment.Console;
using Solve.ProcessingSchemes.Dataflow;
using System.Text;
using System.Threading.Tasks;

namespace Eater
{
	internal class Runner : RunnerBase<Genome>
	{
		// Keep some known viable genomes for reintroduction.
		public static readonly string[] Seed =
		{
			"^^^^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^>^^^^^^^^>^^^^^^^>^^^^^^^>^^^^^^>^^^^^^>^^^^^>^^^^^>^^^^>^^^^>^^^>^^^>^^>^^>^>^",
			"^^^>^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^^>^^^^^^^^>^^^^^^^^>^^^^^^^>^^^^^^^>^^^^^^>^^^^^^>^^^^^>^^^^^>^^^^>^^^^>^^^>^^^>^^>^^>^>^^^^^>^^^^^>^^^^^^^^^>^^^^^^^>^^",
			"3^>6^>9^>9^>9^>8^>8^>7^>7^>6^>6^>5^>5^>4^>4^>3^>3^>2^>2^>^>5^>5^>9^>5^"
		};

		readonly ushort _minSamples;
		//readonly ushort _minConvSamples;

		protected Runner(ushort minSamples/*, ushort minConvSamples = 20*/)
		{
			_minSamples = minSamples;
			//_minConvSamples = minConvSamples;
		}

		static Genome GenerateIdealSeed(ushort size)
		{
			var s = size - 1;
			var sb = new StringBuilder();
			sb.Append(s).Append('^');

			for (var i = 0; i < 2; i++)
				sb
					.Append('>').Append(s).Append('^');

			for (; s > 0; s--)
				sb
					.Append('>').Append(s).Append('^')
					.Append('>').Append(s).Append('^');

			return new Genome(sb.ToString());
		}

		public void Init(bool startWithIdealSeed = false)
		{
			const ushort size = 10;

			var emitter = new EaterConsoleEmitter(_minSamples);
			var seed = startWithIdealSeed ? GenerateIdealSeed(size) : null;
			if (seed != null) emitter.SaveGenomeImage(seed, "LatestSeed");

			var factory = new GenomeFactory(seed, leftTurnDisabled: true);
			var scheme = new DataflowScheme<Genome>(factory, (800, 40, 2));
			// ReSharper disable once RedundantArgumentDefaultValue
			scheme.AddProblem(Problem.CreateF0102(size));
			//scheme.AddProblem(EaterProblem.CreateF02(10, 40));

			Init(scheme, emitter, factory.Metrics);

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

		static Task Main()
		{
			var runner = new Runner(10);
			runner.Init();
			var message = string.Format(
				"Solving Eater Problem... (minimum {0:n0} samples before displaying)",
				runner._minSamples);
			return runner.Start(message);
		}

	}
}
