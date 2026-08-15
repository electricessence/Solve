using Solve.Evaluation;
using Solve.Experiment.Console;
using Solve.Metrics;
using Solve.ProcessingSchemes;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Multiplexer;

[SuppressMessage("ReSharper", "UnusedMember.Local")]
internal class Runner : RunnerBase<EvalGenome<bool>>
{
	/// <summary>2-line multiplexer: 1 address bit selects between 2 data bits.</summary>
	static bool Mux2(IReadOnlyList<bool> p)
		=> p[0] ? p[2] : p[1];

	/// <summary>4-line multiplexer: 2 address bits select among 4 data bits.</summary>
	static bool Mux4(IReadOnlyList<bool> p)
	{
		var index = (p[0] ? 1 : 0) | (p[1] ? 2 : 0);
		return index switch
		{
			0 => p[2],
			1 => p[3],
			2 => p[4],
			_ => p[5],
		};
	}

	readonly ushort _minSamples;

	protected Runner(ushort minSamples, ushort minConvSamples = 20) : base(minSamples > minConvSamples ? minSamples : minConvSamples)
	{
		_minSamples = minSamples;
	}

	public void Init()
	{
		var metrics = new CounterRegistry();
		var factory = new BooleanEvalGenomeFactory(metrics);
		var emitter = new EvalConsoleEmitter(factory, _minSamples);

		// PoolSize mirrors BlackBoxFunction's TowerScheme sizing (800, 80, 2) -- same
		// tapering shape (first, minimum, step). MaxLevels is capped explicitly (as in
		// Eater's Console/Runner.cs) rather than left at the ushort.MaxValue default:
		// BooleanEvalGenomeFactory's structural mutation intentionally returns null
		// (not yet implemented), so this tower only ever advances via generation,
		// variation, and crossover -- bounding level count keeps an unconverged run
		// from growing towers without limit while that gap exists.
		var config = new SchemeConfig
		{
			PoolSize = (800, 80, 2),
			MaxLevels = 500,
		};
		var scheme = new TowerScheme<EvalGenome<bool>>(factory, config);
		scheme.AddProblem(Problem.Create(Mux4, 100));

		Init(scheme, emitter, metrics);
	}

	static Task Main()
	{
		var runner = new Runner(1);
		runner.Init();
		var message = string.Format(
			"Solving Multiplexer Problem... (minimum {0:n0} samples before displaying)",
			runner._minSamples);
		return runner.Start(message);
	}
}
