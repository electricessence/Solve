using BlackBoxFunction;
using Open.Evaluation.Catalogs;
using Open.Evaluation.Core;
using Solve.Evaluation;
using Solve.Experiment.Console;
using Solve.Metrics;
using Spectre.Console;
using Spectre.Console.Rendering;
using Spectre.Console.Testing;
using System.Collections.Immutable;
using System.Globalization;

namespace Solve.Tests;

/// <summary>
/// Covers task 15-0039: BlackBoxFunction's problem-specific live-display panels (expression text,
/// Direction/Correlation gauges, Divergence/gene-count readouts, convergence banner) built by
/// <see cref="BlackBoxLiveView"/> and plugged into <see cref="RunnerDisplay.BuildLayout"/> via
/// <see cref="EvalConsoleEmitter.BuildExtraPanels"/> (task 15-0037/15-0038's generic extension
/// hook on <see cref="ConsoleEmitterBase{TGenome}"/>). Mirrors <c>SpectreRenderingTests</c>'
/// approach: drive the real emitter API with hand-built data and assert on rendered
/// <see cref="TestConsole"/> output, no real environment/scheme required.
/// </summary>
public class BlackBoxLiveViewTests
{
	// Metric definitions matching BlackBoxFunction.Problem's Metrics01/02/03 exactly (name, format,
	// max value, tolerance) -- duplicated here rather than referenced because Problem.Metrics01 is
	// `protected` (and Problem.cs is off-limits to edit for this task; another agent is actively
	// working in it). Deliberately assembled in Metrics02's order (Correlation, Divergence,
	// Direction, Gene-Count) -- NOT Metrics01's (Direction, Correlation, Divergence, Gene-Count) --
	// to prove BlackBoxLiveView.BuildSnapshot looks metrics up by Metric.Name, not position.
	private static readonly Metric DirectionMetric = new(0, "Direction", "Direction {0:p1}", 1, 1e-7);
	private static readonly Metric CorrelationMetric = new(1, "Correlation", "Correlation {0:p10}", 1, 1e-7);
	private static readonly Metric DivergenceMetric = new(2, "Divergence", "Divergence {0:n1}", 0, 0.0000000000001);
	private static readonly Metric GeneCountMetric = new(3, "Gene-Count", "Gene-Count {0:n0}");

	private static readonly ImmutableArray<Metric> PermutedMetrics =
		[CorrelationMetric, DivergenceMetric, DirectionMetric, GeneCountMetric];

	/// <param name="divergence">Positive raw divergence magnitude (smaller is better).</param>
	private static Fitness MakeFitness(int sampleCount, double direction, double correlation, double divergence, int geneCount)
	{
		var fitness = new Fitness(PermutedMetrics);
		// ProcedureResults stores the SUM over `count` samples -- scale each value by sampleCount so
		// the resulting *average* equals the value passed in (see FitnessTests.cs's own Merge
		// helper/comment). Values ordered to match PermutedMetrics: Correlation, Divergence,
		// Direction, Gene-Count. Problem.cs stores Divergence/Gene-Count negated into the
		// maximized-fitness space.
		fitness.Merge(ImmutableArray.Create(
			correlation * sampleCount,
			-divergence * sampleCount,
			direction * sampleCount,
			-(double)geneCount * sampleCount), sampleCount);
		return fitness;
	}

	private sealed class FakeProblemPool(ImmutableArray<Metric> metrics) : IProblemPool<EvalGenome<double>>
	{
		public ImmutableArray<Metric> Metrics { get; } = metrics;
		public Func<EvalGenome<double>, double[], Fitness> Transform { get; } = (_, values) => new Fitness(metrics, values);
		public (EvalGenome<double> Genome, Fitness? Fitness) BestFitness { get; private set; }

		public bool UpdateBestFitness(EvalGenome<double> genome, Fitness fitness)
		{
			BestFitness = (genome, fitness);
			return true; // Every reported fitness is treated as a new champion for these tests.
		}

		public RankedPool<EvalGenome<double>> Champions { get; } = new(2);
	}

	private sealed class FakeProblem(int id) : IProblem<EvalGenome<double>>
	{
		public int ID { get; } = id;
		public IReadOnlyList<IProblemPool<EvalGenome<double>>> Pools { get; } = [new FakeProblemPool(PermutedMetrics)];
		public IEnumerable<Fitness> ProcessSample(EvalGenome<double> g, long sampleId) => [];
		public ValueTask<IEnumerable<Fitness>> ProcessSampleAsync(EvalGenome<double> g, long sampleId) => new(ProcessSample(g, sampleId));
		public long TestCount { get; } = 100;
		public bool HasConverged => false;
		public void Converged() { }
	}

	#region BlackBoxLiveView.BuildSnapshot -- pure metric extraction / convergence pinning

	[Fact]
	public void BuildSnapshot_LooksUpMetricsByName_NotPosition()
	{
		Fitness fitness = MakeFitness(sampleCount: 60, direction: 0.9123456789, correlation: 0.8123456789, divergence: 1.5, geneCount: 12);

		BlackBoxLiveView.PoolSnapshot snapshot = BlackBoxLiveView.BuildSnapshot(
			fitness, minConvergenceSamples: 20, elapsed: TimeSpan.FromSeconds(5), previousConvergedAt: null);

		Assert.Equal(0.9123456789, snapshot.Direction, precision: 10);
		Assert.Equal(0.8123456789, snapshot.Correlation, precision: 10);
		Assert.Equal(1.5, snapshot.Divergence, precision: 10);
		Assert.False(snapshot.Converged);
		Assert.Null(snapshot.ConvergedAt);
	}

	[Fact]
	public void BuildSnapshot_WithinTolerance_ReportsConvergedAndPinsElapsed()
	{
		Fitness fitness = MakeFitness(sampleCount: 60, direction: 1.0, correlation: 1.0, divergence: 0.0, geneCount: 5);

		BlackBoxLiveView.PoolSnapshot first = BlackBoxLiveView.BuildSnapshot(
			fitness, minConvergenceSamples: 20, elapsed: TimeSpan.FromSeconds(10), previousConvergedAt: null);
		Assert.True(first.Converged);
		Assert.Equal(TimeSpan.FromSeconds(10), first.ConvergedAt);

		// A later render tick reuses the pinned ConvergedAt rather than drifting to "now".
		BlackBoxLiveView.PoolSnapshot second = BlackBoxLiveView.BuildSnapshot(
			fitness, minConvergenceSamples: 20, elapsed: TimeSpan.FromSeconds(25), previousConvergedAt: first.ConvergedAt);
		Assert.True(second.Converged);
		Assert.Equal(TimeSpan.FromSeconds(10), second.ConvergedAt);
	}

	[Fact]
	public void BuildSnapshot_BelowMinConvergenceSamples_NeverConvergedRegardlessOfValues()
	{
		Fitness fitness = MakeFitness(sampleCount: 5, direction: 1.0, correlation: 1.0, divergence: 0.0, geneCount: 5);

		BlackBoxLiveView.PoolSnapshot snapshot = BlackBoxLiveView.BuildSnapshot(
			fitness, minConvergenceSamples: 20, elapsed: TimeSpan.Zero, previousConvergedAt: null);

		Assert.False(snapshot.Converged);
		Assert.Null(snapshot.ConvergedAt);
	}

	#endregion

	#region BlackBoxLiveView.BuildExpressionPanel -- truncation (AC1)

	[Fact]
	public void BuildExpressionPanel_LongExpression_TruncatesWithIndicator()
	{
		string longExpression = new string('a', BlackBoxLiveView.MaxExpressionLength + 50);

		IRenderable panel = BlackBoxLiveView.BuildExpressionPanel(longExpression);
		var console = new TestConsole();
		console.Width(4000);
		console.Write(panel);
		string output = console.Output;

		Assert.Contains("truncated", output, StringComparison.Ordinal);
		Assert.Contains((BlackBoxLiveView.MaxExpressionLength + 50).ToString("n0", CultureInfo.InvariantCulture), output, StringComparison.Ordinal);
	}

	[Fact]
	public void BuildExpressionPanel_ShortExpression_RendersInFullWithoutTruncationIndicator()
	{
		const string expression = "a + b";

		IRenderable panel = BlackBoxLiveView.BuildExpressionPanel(expression);
		var console = new TestConsole();
		console.Width(200);
		console.Write(panel);
		string output = console.Output;

		Assert.Contains(expression, output, StringComparison.Ordinal);
		Assert.DoesNotContain("truncated", output, StringComparison.Ordinal);
	}

	#endregion

	#region End-to-end through EvalConsoleEmitter + RunnerDisplay.BuildLayout (AC4)

	/// <summary>
	/// AC4: drives a real champion event (<see cref="ConsoleEmitterBase{TGenome}.EmitTopGenomeStats"/>)
	/// with known fitness values through <see cref="EvalConsoleEmitter"/>, pulls its
	/// <see cref="EvalConsoleEmitter.BuildExtraPanels"/> output into <see
	/// cref="RunnerDisplay.BuildLayout"/> exactly the way <c>RunnerBase.BuildRenderable</c> does, and
	/// asserts the rendered <see cref="TestConsole"/> output contains the expression text (whatever
	/// EvalConsoleEmitter's existing alpha-formatting actually produced -- not re-derived here) plus
	/// the Direction/Correlation gauge values at 10 significant digits, and the "Searching" banner
	/// (fitness deliberately short of convergence).
	/// </summary>
	[Fact]
	public void BuildExtraPanels_RenderedThroughLayout_ContainsExpressionAndGaugeValues()
	{
		var metrics = new CounterRegistry();
		var factory = new NumericEvalGenomeFactory(metrics);
		var emitter = new EvalConsoleEmitter(factory, sampleMinimum: 1);

		var genome = new EvalGenome<double>(factory.Catalog.GetParameter(0));
		var problem = new FakeProblem(id: 9);

		const double direction = 0.9123456789;
		const double correlation = 0.8123456789;
		Fitness fitness = MakeFitness(sampleCount: 60, direction, correlation, divergence: 1.5, geneCount: 3);

		emitter.EmitTopGenomeStats((genome, fitness, problem, PoolIndex: 0));

		TopGenomeStat stat = Assert.Single(emitter.TopGenomeStats).Value;
		// The single-line formatted expression EvalConsoleEmitter actually produced (GenomeText is
		// multi-line -- "Genome:\n<blank>\n<formatted>" -- so pull just the trailing content line;
		// a Panel border would otherwise interrupt an embedded-newline substring match).
		string expressionLine = stat.GenomeText
			.Split('\n')
			.Select(l => l.Trim())
			.Last(l => l.Length > 0);

		IReadOnlyList<IRenderable> extraPanels = emitter.BuildExtraPanels();
		Assert.Single(extraPanels);

		IRenderable layout = RunnerDisplay.BuildLayout(
			info: "Solving BlackBox Test...",
			elapsed: TimeSpan.FromSeconds(7),
			problemStats: [(problem.ID, problem.TestCount, 5L)],
			noveltySaturation: 0.05,
			topGenomeStats: emitter.TopGenomeStats,
			recentEvents: [],
			extraPanels: extraPanels);

		var console = new TestConsole();
		console.Width(300);
		// The default TestConsole height (24, a typical terminal) isn't tall enough for
		// Header(5) + Events(10) + a real "Extra" panel stacked with Status/Top-Genomes -- Spectre
		// clips a Layout region's content to whatever height it's allotted rather than scrolling,
		// so a short console silently drops the panel's later rows. Tall enough that Header(5) +
		// Stats + Extra (one BlackBox panel: banner/expression/2 gauges/2 readouts, ~9 rows plus
		// borders) + Events(10) all fit without clipping.
		console.Height(80);
		console.Write(layout);
		string output = console.Output;

		Assert.Contains(expressionLine, output, StringComparison.Ordinal);
		Assert.Contains(direction.ToString("G10", CultureInfo.InvariantCulture), output, StringComparison.Ordinal);
		Assert.Contains(correlation.ToString("G10", CultureInfo.InvariantCulture), output, StringComparison.Ordinal);
		Assert.Contains("Searching", output, StringComparison.Ordinal);
	}

	/// <summary>AC3: once Direction/Correlation/Divergence all sit within tolerance, the banner switches to "Converged".</summary>
	[Fact]
	public void BuildExtraPanels_ConvergedFitness_ShowsConvergedBanner()
	{
		var metrics = new CounterRegistry();
		var factory = new NumericEvalGenomeFactory(metrics);
		var emitter = new EvalConsoleEmitter(factory, sampleMinimum: 1);

		var genome = new EvalGenome<double>(factory.Catalog.GetParameter(0));
		var problem = new FakeProblem(id: 11);

		Fitness fitness = MakeFitness(sampleCount: 60, direction: 1.0, correlation: 1.0, divergence: 0.0, geneCount: 1);
		emitter.EmitTopGenomeStats((genome, fitness, problem, PoolIndex: 0));

		IReadOnlyList<IRenderable> extraPanels = emitter.BuildExtraPanels();

		var console = new TestConsole();
		console.Width(300);
		foreach (IRenderable panel in extraPanels)
			console.Write(panel);
		string output = console.Output;

		Assert.Contains("Converged", output, StringComparison.Ordinal);
		Assert.DoesNotContain("Searching", output, StringComparison.Ordinal);
	}

	/// <summary>
	/// Task 15-0037 AC5 / task 15-0039 AC5: redirected/non-interactive rendering (the same path a
	/// killed, output-redirected BlackBoxFunction process takes) must not throw, whether or not any
	/// extra panels are present.
	/// </summary>
	[Fact]
	public void WritePlainStatus_WithExtraPanels_DoesNotThrow()
	{
		var metrics = new CounterRegistry();
		var factory = new NumericEvalGenomeFactory(metrics);
		var emitter = new EvalConsoleEmitter(factory, sampleMinimum: 1);

		var genome = new EvalGenome<double>(factory.Catalog.GetParameter(0));
		var problem = new FakeProblem(id: 4);
		Fitness fitness = MakeFitness(sampleCount: 60, direction: 0.5, correlation: 0.5, divergence: 3.0, geneCount: 2);
		emitter.EmitTopGenomeStats((genome, fitness, problem, PoolIndex: 0));

		var console = new TestConsole();
		Assert.False(console.Profile.Capabilities.Interactive);

		Exception? thrown = Record.Exception(() => RunnerDisplay.WritePlainStatus(
			console,
			info: "Solving BlackBox Test...",
			elapsed: TimeSpan.FromSeconds(3),
			problemStats: [(problem.ID, problem.TestCount, 2L)],
			noveltySaturation: 0.1,
			topGenomeStats: emitter.TopGenomeStats,
			extraPanels: emitter.BuildExtraPanels()));

		Assert.Null(thrown);
		Assert.Contains("Novelty saturation", console.Output, StringComparison.Ordinal);
	}

	#endregion

	#region Generic hook default (task 15-0037/15-0038's ConsoleEmitterBase.BuildExtraPanels)

	[Fact]
	public void ConsoleEmitterBase_BuildExtraPanels_DefaultsToEmpty()
	{
		var emitter = new ConsoleEmitterBase<EvalGenome<double>>(sampleMinimum: 1);
		Assert.Empty(emitter.BuildExtraPanels());
	}

	#endregion
}
