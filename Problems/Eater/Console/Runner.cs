using App.Metrics;
using Open.Disposable;
using Solve.Experiment.Console;
using Solve.ProcessingSchemes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Eater;

class Runner : RunnerBase<Genome>
{
	readonly ushort _size;

	//readonly ushort _minConvSamples;
	readonly Lazy<EaterConsoleEmitter> _emitter;

	const bool leftTurnDisabled = true;
	bool _init;

	protected Runner(ushort size, ushort minSamples = 10/*, ushort minConvSamples = 20*/)
	{
		_size = size;
		_emitter = new Lazy<EaterConsoleEmitter>(() => EaterConsoleEmitter.Create(minSamples));
		//_minConvSamples = minConvSamples;
	}

	static string GenerateIdealSeed(ushort size)
		=> StringBuilderPool.RentToString(sb =>
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
		_emitter.Value.SaveGenomeImage(ideal, "IdealSeed");
		Init(ideal);
	}

	public void InitPreviousWinners() => Init(_emitter.Value.PreviousWinners);
	public async ValueTask InitSeedsAsync() => Init(await Seeds().ToArrayAsync());
	public void Init(params string[] seeds) => Init(seeds ?? Enumerable.Empty<string>());
	public void Init(IEnumerable<string> seeds)
	{
		if (_init) throw new InvalidOperationException("Can only initialize once.");
		_init = true;

		var seedGenomes = seeds
			.Concat(GenomeFactory.Random(100, _size * 2, leftTurnDisabled).Take(1000).AsParallel())
			.Distinct().Select(s => new Genome(s));
		var metrics = new MetricsBuilder().Build();
		var factory = new GenomeFactory(metrics.Provider.Counter, seedGenomes, leftTurnDisabled: leftTurnDisabled);
		//var scheme = new Solve.ProcessingSchemes.Dataflow.DataflowScheme<Genome>(metrics, factory, (800, 40, 2));
		var config = new SchemeConfig
		{
			MaxLevels = 500,
			PoolSize = (400, 40, 2),
		};
		var scheme = new Solve.ProcessingSchemes.TowerScheme<Genome>(factory, config);
		// ReSharper disable once RedundantArgumentDefaultValue
		scheme.AddProblem(Problem.CreateFitnessSecondary(_size));
		//scheme.AddProblem(EaterProblem.CreateF02(10, 40));

		Init(scheme, _emitter.Value, metrics);

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
		while (null != (line = await reader.ReadLineAsync()))
		{
			if (string.IsNullOrWhiteSpace(line)) continue;
			yield return line;
		}
	}

	static Task Main(params string[] args)
	{
		ushort size = 10;
		if (args.Length != 0) size = ushort.Parse(args[0]);
		return Start(size).completion;
	}

	public static (Runner runner, Task completion) Start(ushort size)
	{
		//Console.WriteLine("Press any key to start.");
		//Console.ReadLine();
		//Console.Clear();
		var runner = new Runner(size, 2);
		//await runner.InitSeedsAsync();
		runner.InitPreviousWinners();

		var message = string.Format(
			"Solving Eater Problem... (minimum {0:n0} samples before displaying)",
			runner._emitter.Value.SampleMinimum);

		return (runner, runner.Start(message));
	}

}
