using Eater;
using Solve.Experiment.Console;
using Spectre.Console.Rendering;
using Spectre.Console.Testing;
using System.Collections.Immutable;
using System.Drawing;

namespace Solve.Tests;

/// <summary>
/// Task 15-0038: <see cref="EaterLiveView"/> is deliberately pure/side-effect-free (no real
/// <c>Eater.Problem</c>, no <see cref="EaterConsoleEmitter"/> disk writes) -- exactly the same
/// shape as <c>SpectreRenderingTests</c> drives <c>RunnerDisplay</c> with hand-built data instead
/// of a real environment/scheme. These tests cover the AC2 champion-selection rule directly, then
/// AC5's "one champion event, rendered through a TestConsole" requirement first against
/// <see cref="EaterLiveView"/> alone and then through <see cref="RunnerDisplay.BuildLayout"/>'s
/// generic extra-panels hook, proving the two compose without either needing to know about the
/// other's internals.
/// </summary>
public class EaterLiveViewTests
{
	private static readonly Size Grid10 = new(10, 10);

	// --- AC2: cross-pool champion-selection rule ------------------------------------------------

	[Fact]
	public void IsBetterChampion_NothingRecordedYet_AnyCandidateWins()
	{
		var candidate = new EaterLiveView.ChampionRank(FoodFoundRate: 0.1, GeneCount: 999);
		Assert.True(EaterLiveView.IsBetterChampion(candidate, currentBest: null));
	}

	[Fact]
	public void IsBetterChampion_PerfectScorer_BeatsNonPerfect_RegardlessOfGeneCount()
	{
		var perfectButLarger = new EaterLiveView.ChampionRank(FoodFoundRate: 1.0, GeneCount: 500);
		var nonPerfectSmaller = new EaterLiveView.ChampionRank(FoodFoundRate: 0.9, GeneCount: 10);

		Assert.True(EaterLiveView.IsBetterChampion(perfectButLarger, nonPerfectSmaller));
		Assert.False(EaterLiveView.IsBetterChampion(nonPerfectSmaller, perfectButLarger));
	}

	[Fact]
	public void IsBetterChampion_AmongPerfectScorers_SmallerGeneCountWins()
	{
		var current = new EaterLiveView.ChampionRank(FoodFoundRate: 1.0, GeneCount: 50);
		var fewerGenes = new EaterLiveView.ChampionRank(FoodFoundRate: 1.0, GeneCount: 30);
		var moreGenes = new EaterLiveView.ChampionRank(FoodFoundRate: 1.0, GeneCount: 70);

		Assert.True(EaterLiveView.IsBetterChampion(fewerGenes, current));
		Assert.False(EaterLiveView.IsBetterChampion(moreGenes, current));
	}

	[Fact]
	public void IsBetterChampion_AmongNonPerfectScorers_FallsBackToHigherRate()
	{
		var current = new EaterLiveView.ChampionRank(FoodFoundRate: 0.6, GeneCount: 10);
		var higherRateButMoreGenes = new EaterLiveView.ChampionRank(FoodFoundRate: 0.8, GeneCount: 200);
		var lowerRateFewerGenes = new EaterLiveView.ChampionRank(FoodFoundRate: 0.4, GeneCount: 5);

		Assert.True(EaterLiveView.IsBetterChampion(higherRateButMoreGenes, current));
		Assert.False(EaterLiveView.IsBetterChampion(lowerRateFewerGenes, current));
	}

	// --- AC5: TestConsole rendering ------------------------------------------------------------

	/// <summary>
	/// AC5: "a TestConsole unit test drives one champion event and asserts the grid panel plus
	/// card content appear." One champion (one <see cref="EaterLiveView.ChampionCard"/>, one
	/// path trace) is built by hand and rendered through <see cref="EaterLiveView.BuildPanels"/>
	/// directly, with no real problem/scheme/emitter involved.
	/// </summary>
	[Fact]
	public void BuildPanels_OneChampionEvent_RenderedThroughTestConsole_ContainsGridAndCardContent()
	{
		// "3^>3^" expands to Forward x3, TurnRight, Forward x3 -- a small, known L-shaped path.
		Genome genome = Genome.Parse("3^>3^");
		ImmutableArray<Point> trace = genome.Trace(Grid10, EaterLiveView.RepresentativeStart);

		const string hash = "abcdef0123456789fedcba9876543210";
		var card = new EaterLiveView.ChampionCard(
			ProblemId: 3, PoolIndex: 0, Hash: hash, GeneCount: 137, SampleCount: 60,
			FoodFoundRate: 0.875, AverageEnergy: -12.5);

		IReadOnlyList<IRenderable> panels = EaterLiveView.BuildPanels(
			Grid10, trace,
			foodExamples: [new Point(9, 9)],
			start: EaterLiveView.RepresentativeStart,
			cards: [card],
			geneCountTrend: [180, 160, 137]);

		Assert.Equal(3, panels.Count);

		var console = new TestConsole();
		console.Width(200); // avoid wrapping split-second substring assertions across lines.
		foreach (IRenderable panel in panels)
			console.Write(panel);

		string output = console.Output;

		// Panel headers (grid canvas, champion cards, trend).
		Assert.Contains("Champion Path", output, StringComparison.Ordinal);
		Assert.Contains("Champion Cards", output, StringComparison.Ordinal);
		Assert.Contains("Gene-Count Trend", output, StringComparison.Ordinal);

		// Card content: pool label, truncated hash, gene count, Food-Found-Rate percentage, sample count.
		Assert.Contains("3.0", output, StringComparison.Ordinal);
		Assert.Contains(hash[..16], output, StringComparison.Ordinal);
		Assert.Contains("137", output, StringComparison.Ordinal);
		Assert.Contains("87.5", output, StringComparison.Ordinal);
		Assert.Contains("60", output, StringComparison.Ordinal);

		// Trend content: the improvement history's most recent (smallest) value.
		Assert.Contains("137", output, StringComparison.Ordinal);
	}

	/// <summary>
	/// Proves task 15-0038's Eater panels compose with task 15-0037's generic layout through the
	/// shared <see cref="ConsoleEmitterBase{TGenome}.BuildExtraPanels"/> hook's consumer,
	/// <see cref="RunnerDisplay.BuildLayout"/>: both the pre-existing header/stats content and the
	/// Eater-specific panels appear in the same rendered output.
	/// </summary>
	[Fact]
	public void RunnerDisplay_BuildLayout_WithEaterExtraPanels_ContainsBothGenericAndEaterContent()
	{
		Genome genome = Genome.Parse("^"); // trivial one-step genome; still a valid path to trace.
		ImmutableArray<Point> trace = genome.Trace(Grid10, EaterLiveView.RepresentativeStart);

		var card = new EaterLiveView.ChampionCard(
			ProblemId: 1, PoolIndex: 0, Hash: "0123456789abcdef0123456789abcdef", GeneCount: 42,
			SampleCount: 55, FoodFoundRate: 1.0, AverageEnergy: -3.25);

		IReadOnlyList<IRenderable> extraPanels = EaterLiveView.BuildPanels(
			Grid10, trace, foodExamples: [], start: EaterLiveView.RepresentativeStart,
			cards: [card], geneCountTrend: [50, 42]);

		IRenderable layout = RunnerDisplay.BuildLayout(
			info: "Solving Eater Problem...",
			elapsed: TimeSpan.FromSeconds(9),
			problemStats: [(1, 999, 5)],
			noveltySaturation: 0.2,
			topGenomeStats: new Dictionary<string, TopGenomeStat>(),
			recentEvents: [],
			extraPanels: extraPanels);

		var console = new TestConsole();
		// The combined layout stacks Header/Stats/Extra/Events as rows; Stats and Extra split
		// whatever height remains after Header's and Events' fixed sizes, so a tall-enough console
		// is needed for the grid canvas plus card/trend panels inside "Extra" to actually render
		// instead of being clipped to nothing (Width alone, as SpectreRenderingTests uses, only
		// prevents horizontal wrapping).
		console.Width(220);
		console.Height(80);
		console.Write(layout);
		string output = console.Output;

		// Task 15-0037's generic header content is unaffected by the extra panels being present.
		Assert.Contains("Solving Eater Problem", output, StringComparison.Ordinal);
		Assert.Contains(999L.ToString("n0"), output, StringComparison.Ordinal);

		// Task 15-0038's Eater-specific content rendered alongside it.
		Assert.Contains("Champion Path", output, StringComparison.Ordinal);
		Assert.Contains("Champion Cards", output, StringComparison.Ordinal);
		Assert.Contains("42", output, StringComparison.Ordinal);
	}

	[Fact]
	public void BuildLayout_WithoutExtraPanels_IsUnaffectedByTheHookExisting()
	{
		// Backward-compatibility check for the shared hook itself: omitting extraPanels (as every
		// pre-15-0038/15-0039 call site does) must reproduce the exact same layout as before.
		IRenderable layout = RunnerDisplay.BuildLayout(
			info: "Solving Eater Problem...",
			elapsed: TimeSpan.FromSeconds(1),
			problemStats: [],
			noveltySaturation: 0d,
			topGenomeStats: new Dictionary<string, TopGenomeStat>(),
			recentEvents: []);

		var console = new TestConsole();
		console.Width(200);
		console.Write(layout);

		Assert.DoesNotContain("Champion Path", console.Output, StringComparison.Ordinal);
	}

	// --- AC4: gene-count trend ------------------------------------------------------------------

	[Fact]
	public void BuildGeneCountTrend_NoEntries_ShowsPlaceholderInsteadOfAnEmptyPanel()
	{
		IRenderable panel = EaterLiveView.BuildGeneCountTrend([]);

		var console = new TestConsole();
		console.Width(100);
		console.Write(panel);

		Assert.Contains("no perfect scorer yet", console.Output, StringComparison.Ordinal);
	}

	[Fact]
	public void BuildGeneCountTrend_RetainsAtLeastTenEntries_WhenGivenThatMany()
	{
		int[] improvements = [.. Enumerable.Range(0, 12).Select(i => 200 - i * 10)]; // 12 improving values.

		IRenderable panel = EaterLiveView.BuildGeneCountTrend(improvements);

		var console = new TestConsole();
		console.Width(100);
		console.Write(panel);
		string output = console.Output;

		foreach (int value in improvements)
			Assert.Contains(value.ToString("n0"), output, StringComparison.Ordinal);
	}

	// --- AC3: champion cards ---------------------------------------------------------------------

	[Fact]
	public void BuildChampionCards_MissingMetric_RendersNotAvailableInsteadOfNaN()
	{
		var card = new EaterLiveView.ChampionCard(
			ProblemId: 2, PoolIndex: 1, Hash: "hash", GeneCount: 5, SampleCount: 10,
			FoodFoundRate: double.NaN, AverageEnergy: double.NaN);

		IRenderable panel = EaterLiveView.BuildChampionCards([card]);

		var console = new TestConsole();
		console.Width(120);
		console.Write(panel);
		string output = console.Output;

		Assert.Contains("n/a", output, StringComparison.Ordinal);
		Assert.DoesNotContain("NaN", output, StringComparison.Ordinal);
	}
}
