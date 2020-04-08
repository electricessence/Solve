﻿using Solve;
using Solve.Experiment.Console;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Eater
{
	internal class Runner : RunnerBase<Genome>
	{
		readonly ushort _size;

		//readonly ushort _minConvSamples;
		readonly EaterConsoleEmitter _emitter;

		const bool leftTurnDisabled = true;

		protected Runner(ushort size, ushort minSamples = 10/*, ushort minConvSamples = 20*/)
		{
			_size = size;
			_emitter = new EaterConsoleEmitter(minSamples);
			//_minConvSamples = minConvSamples;
		}

		static string GenerateIdealSeed(ushort size)
			=> StringBuilderPool.Rent(sb =>
			{
				var s = size - 1;
				sb.Append(s).Append('^');

				for (var i = 0; i < 2; i++)
					sb.Append('>').Append(s).Append('^');

				for (; s > 0; s--)
					sb
					.Append('>').Append(s).Append('^')
					.Append('>').Append(s).Append('^');
			});

		public void InitIdealSeed()
		{
			var ideal = GenerateIdealSeed(_size);
			_emitter.SaveGenomeImage(ideal, "IdealSeed");
			Init(ideal);
		}

		public void Init(params string[] seeds)
		{
			var seedGenomes = seeds
				.Concat(GenomeFactory.Random(100, _size * 2, leftTurnDisabled).Take(1000).AsParallel())
				.Distinct().Select(s => new Genome(s));
			var factory = new GenomeFactory(seedGenomes, leftTurnDisabled: leftTurnDisabled);
			//var scheme = new Solve.ProcessingSchemes.Dataflow.DataflowScheme<Genome>(factory, (800, 40, 2));
			var scheme = new Solve.ProcessingSchemes.Tower.TowerProcessingScheme<Genome>(factory, (800, 40, 2));
			// ReSharper disable once RedundantArgumentDefaultValue
			scheme.AddProblem(Problem.CreateF0102(_size));
			//scheme.AddProblem(EaterProblem.CreateF02(10, 40));

			Init(scheme, _emitter, factory.Metrics);

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

		static async IAsyncEnumerable<string> Seeds()
		{
			const string fileName = "Seeds.txt";
			if (!File.Exists(fileName))
				yield break;

			using var reader = File.OpenText(fileName);
			string? line;
			while(null!=(line = await reader.ReadLineAsync()))
			{
				if (string.IsNullOrWhiteSpace(line)) continue;
				yield return line;
			}
		}

		static async Task Main()
		{
			//Console.WriteLine("Press any key to start.");
			//Console.ReadLine();
			//Console.Clear();
			var runner = new Runner(20);
			runner.Init(await Seeds().ToArrayAsync());

			var message = string.Format(
				"Solving Eater Problem... (minimum {0:n0} samples before displaying)",
				runner._emitter.SampleMinimum);

			await runner.Start(message);
		}

	}
}
