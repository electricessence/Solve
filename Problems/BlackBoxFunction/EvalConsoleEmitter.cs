using Open.Evaluation.Core;
using Solve;
using Solve.Evaluation;
using Solve.Experiment.Console;
using Spectre.Console.Rendering;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace BlackBoxFunction;

public partial class EvalConsoleEmitter(ICatalog<IEvaluate<double>> catalog, uint sampleMinimum = 50)
	: ConsoleEmitterBase<EvalGenome<double>>(sampleMinimum)
{
	readonly ICatalog<IEvaluate<double>> Catalog = catalog;

	// 15-0039: per-pool metric snapshots (Direction/Correlation/Divergence + convergence) backing
	// BuildExtraPanels() below -- TopGenomeStats (inherited) only keeps a formatted summary string,
	// not the individual values BlackBoxLiveView's gauges/readouts need. Keyed identically to
	// TopGenomeStats ("{ProblemId}.{PoolIndex}") and populated from the same OnEmittingGenomeFitness
	// call that feeds the base class's own bookkeeping.
	private readonly ConcurrentDictionary<string, BlackBoxLiveView.PoolSnapshot> _snapshots = new();

	// Own stopwatch rather than reusing RunnerBase's: this emitter is constructed independently of
	// (and slightly before) RunnerBase.Start(), and BlackBoxLiveView only needs elapsed time for a
	// "time to convergence" display, not perfectly synced clocks.
	private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

	public EvalConsoleEmitter(NumericEvalGenomeFactory factory, uint sampleMinimum = 50)
		: this(factory.Catalog, sampleMinimum)
	{
	}

	static readonly Regex SimpleProductsPattern = SimpleProductsRegex();
	static readonly Regex StripParensPattern = StripParensRegex();
	//static readonly Regex SuperScriptDigitPattern = new Regex(@"\^[0-9\.]+", RegexOptions.Compiled);
	//static readonly Regex CombineMultiplePattern = new Regex(@"(\d+\s\*\s)[a-z]+", RegexOptions.Compiled);
	//static readonly Regex DivisionPattern = new Regex(@"\((\w+)\^-(\d+)\)", RegexOptions.Compiled);
	//static readonly Regex DivisionTailedPattern = new Regex($@" \* \(1/(\w+[{Exponent.SuperScriptDigits}]*)\)", RegexOptions.Compiled);
	//static readonly Regex NegativeMultiplePattern = new Regex(@" \+ \(-(\w+) ([*/]) ", RegexOptions.Compiled);

	static string FormatGenomeString(string h)
	{
		//h = h
		//	.Replace(" + -", " - ")
		//	.Replace(" + (-1 * ", " - (");
		h = SimpleProductsPattern.Replace(h,
			m => m.Value.Replace(" * ", string.Empty));
		h = StripParensPattern.Replace(h,
			m => m.Groups[1].Value + m.Groups[2].Value);
		h = StripParensPattern.Replace(h,
			m => m.Groups[1].Value + m.Groups[2].Value);
		//h = CombineMultiplePattern.Replace(h,
		//	m => m.Value.Replace(" * ", string.Empty));
		//h = DivisionTailedPattern.Replace(h, m => $" / {m.Groups[1].Value}");
		//h = NegativeMultiplePattern.Replace(h, m => $" - ({m.Groups[1].Value} {m.Groups[2].Value} ");
		return h;
	}

	protected override void OnEmittingGenome(
		EvalGenome<double> genome,
		StringBuilder output)
	{
		//base.OnEmittingGenome(p, genome, fitness, output);
		output
			.Append("Genome:")
			.AppendLine(BLANK)
			.AppendLine(Format(genome.Root));

		if (genome.Root is IReducibleEvaluation<IEvaluate<double>> r && r.TryGetReduced(Catalog, out var reduced))
		{
			output
				.Append("Reduced:")
				.AppendLine(BLANK)
				.AppendLine(Format(reduced));
		}

		static string Format(IEvaluate<double> root)
		{
			var hash = root.ToStringRepresentation();
			var alpha = AlphaParameters.ConvertTo(hash);
			return FormatGenomeString(alpha);
		}
	}

	// 15-0039: captures the raw per-metric values (by name -- Metrics01/02/03 permute
	// Direction/Correlation/Divergence into different slots per pool) that BuildExtraPanels below
	// needs but the base class's TopGenomeStats snapshot doesn't keep. Runs on every champion
	// update, same as the base implementation this calls through to.
	protected override void OnEmittingGenomeFitness(IProblem<EvalGenome<double>> p, EvalGenome<double> genome, int poolIndex, Fitness fitness)
	{
		base.OnEmittingGenomeFitness(p, genome, poolIndex, fitness);

		string key = $"{p.ID}.{poolIndex}";
		TimeSpan elapsed = _stopwatch.Elapsed;
		_snapshots.AddOrUpdate(
			key,
			addValueFactory: _ => BlackBoxLiveView.BuildSnapshot(fitness, SampleMinimum, elapsed, previousConvergedAt: null),
			updateValueFactory: (_, previous) => BlackBoxLiveView.BuildSnapshot(fitness, SampleMinimum, elapsed, previous.ConvergedAt));
	}

	/// <summary>
	/// Task 15-0039: BlackBoxFunction's contribution to the generic extra-panels hook (task
	/// 15-0037/15-0038's <see cref="ConsoleEmitterBase{TGenome}.BuildExtraPanels"/>) -- one panel
	/// per pool with the champion expression, Direction/Correlation gauges, Divergence/gene-count
	/// readouts, and a convergence banner. See <see cref="BlackBoxLiveView"/> for the pure builders.
	/// </summary>
	public override IReadOnlyList<IRenderable> BuildExtraPanels()
		=> BlackBoxLiveView.BuildPanels(TopGenomeStats, _snapshots);

	[GeneratedRegex(@"\((\w+[⁰¹²³⁴⁵⁶⁷⁸⁹]*)\)(\)|\s)", RegexOptions.Compiled)]
	private static partial Regex StripParensRegex();

	[GeneratedRegex(@"(\d+|[a-z]+)(\s\*\s[a-z]+)+", RegexOptions.Compiled)]
	private static partial Regex SimpleProductsRegex();
}
