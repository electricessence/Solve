using Spectre.Console;
using Spectre.Console.Rendering;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.Linq;
// Spectre.Console and System.Drawing both declare Size/Color -- Eater's grid geometry is
// System.Drawing.Size/Point (same as Step.cs/PathTracer.cs), while every Color used here is
// unambiguously Spectre's renderable color, so alias both rather than fully-qualifying every use.
using Size = System.Drawing.Size;
using Color = Spectre.Console.Color;

namespace Eater;

/// <summary>
/// Pure, side-effect-free Spectre.Console builders for Eater's problem-specific live panels (task
/// 15-0038): a champion-path <see cref="Canvas"/>, per-pool champion cards, and a best-gene-count
/// trend indicator. Kept independent of <see cref="EaterConsoleEmitter"/>'s concurrent bookkeeping
/// -- exactly the way <see cref="Solve.Experiment.Console.RunnerDisplay"/> is kept independent of
/// <see cref="Solve.Experiment.Console.ConsoleEmitterBase{TGenome}"/> -- so Solve.Tests can drive
/// these builders directly with hand-built data via a <c>Spectre.Console.Testing.TestConsole</c>,
/// without standing up a real problem/environment or touching disk.
/// </summary>
public static class EaterLiveView
{
	/// <summary>
	/// Single representative start position the champion path canvas traces from (scope boundary:
	/// no multi-start composite preview). A plain constant rather than anything computed, so it is
	/// trivially "configurable" by editing this one value -- and (0,0) is guaranteed valid for any
	/// grid size <see cref="SampleCache"/> accepts (minimum 2x2).
	/// </summary>
	public static readonly Point RepresentativeStart = new(0, 0);

	/// <summary>
	/// The two values <see cref="IsBetterChampion"/> ranks by: task 15-0038 AC2's "best (by gene
	/// count among perfect scorers, falling back to best rate)" rule. Deliberately small and
	/// independent of any live genome/fitness type so it is trivial to hand-build in tests.
	/// </summary>
	/// <param name="FoodFoundRate">Fraction of samples the genome found its food on, in [0, 1].</param>
	/// <param name="GeneCount">The genome's gene count.</param>
	public readonly record struct ChampionRank(double FoodFoundRate, int GeneCount)
	{
		/// <summary>
		/// A genome that finds its food on every sample. Eater's Food-Found-Rate metric is an
		/// exact ratio of integer counts (found / count) with zero tolerance, so this is an exact,
		/// not approximate, comparison.
		/// </summary>
		public bool IsPerfect => FoodFoundRate >= 1.0;
	}

	/// <summary>
	/// Task 15-0038 AC2's cross-pool champion-selection rule for which single genome's path the
	/// canvas renders: a perfect scorer (<see cref="ChampionRank.IsPerfect"/>) always beats a
	/// non-perfect one; among two perfect scorers the smaller gene count wins; among two
	/// non-perfect scorers the higher Food-Found-Rate wins (the "falling back to best rate" half
	/// of AC2). <paramref name="currentBest"/> of <see langword="null"/> means "nothing recorded
	/// yet" -- any candidate is better than that.
	/// </summary>
	public static bool IsBetterChampion(ChampionRank candidate, ChampionRank? currentBest)
	{
		if (currentBest is not { } best) return true;

		if (candidate.IsPerfect != best.IsPerfect)
			return candidate.IsPerfect;

		return candidate.IsPerfect
			? candidate.GeneCount < best.GeneCount
			: candidate.FoodFoundRate > best.FoodFoundRate;
	}

	/// <summary>
	/// Task 15-0038 AC3's per-pool champion card content: pool index, truncated hash, gene count,
	/// Food-Found-Rate, energy, and sample count for one problem/pool's current best genome.
	/// </summary>
	public readonly record struct ChampionCard(
		int ProblemId,
		int PoolIndex,
		string Hash,
		int GeneCount,
		int SampleCount,
		double FoodFoundRate,
		double AverageEnergy);

	/// <summary>
	/// Task 15-0038 AC2: a grid <see cref="Canvas"/> no smaller than <paramref name="boundary"/>,
	/// shading <paramref name="trace"/>'s cells by visit order (a later visit to an already-shaded
	/// cell overwrites its color -- consistent with this being a static, latest-champion-only
	/// render per the scope boundary, not an animation), plus a distinct start-cell marker and
	/// food-example markers.
	/// </summary>
	/// <param name="boundary">The problem's grid size.</param>
	/// <param name="trace">The ordered visited-cell sequence from <see cref="PathTracer.Trace(ImmutableArray{Step}, Size, Point)"/>; may be empty.</param>
	/// <param name="foodExamples">A small set of example food positions to mark, purely for visual reference (not the path's actual destination).</param>
	/// <param name="start">The start cell <paramref name="trace"/> began from.</param>
	public static IRenderable BuildPathCanvas(
		Size boundary,
		ImmutableArray<Point> trace,
		IReadOnlyCollection<Point> foodExamples,
		Point start)
	{
		if (boundary.Width < 1 || boundary.Height < 1)
			throw new ArgumentOutOfRangeException(nameof(boundary), boundary, "Must be at least 1x1.");

		var canvas = new Canvas(boundary.Width, boundary.Height);

		// Background: every cell starts as an unvisited grid square.
		for (int y = 0; y < boundary.Height; y++)
		{
			for (int x = 0; x < boundary.Width; x++)
				canvas.SetPixel(x, ToCanvasY(y, boundary), Color.Grey15);
		}

		// Visited-cell gradient by visit order -- dim near the start, bright near the latest visit.
		int lastIndex = trace.Length - 1;
		for (int i = 0; i < trace.Length; i++)
		{
			Point p = trace[i];
			if (!InBounds(p, boundary)) continue; // Defensive only -- Trace() never leaves the grid.
			double t = lastIndex <= 0 ? 1d : (double)i / lastIndex;
			canvas.SetPixel(p.X, ToCanvasY(p.Y, boundary), VisitGradient(t));
		}

		// Food-example markers.
		foreach (Point food in foodExamples)
		{
			if (InBounds(food, boundary))
				canvas.SetPixel(food.X, ToCanvasY(food.Y, boundary), Color.Red);
		}

		// Start marker drawn last so it is always identifiable, even if the path revisits it.
		if (InBounds(start, boundary))
			canvas.SetPixel(start.X, ToCanvasY(start.Y, boundary), Color.White);

		// Deliberately not .Expand()-ed: this panel is always stacked with the champion-cards and
		// gene-count-trend panels inside one shared Rows() (see BuildPanels), all sharing a single
		// Layout slot. An expanded child there greedily claims the whole slot's height, leaving
		// nothing for its siblings to render into -- so every panel in this file sizes to its own
		// content instead, the same way Rows() already lets BuildEvents' entries stack. A small
		// grid's auto-calculated width (Expand=false's default) can end up narrower than the
		// "Champion Path" header itself, truncating it -- pin a floor wide enough for the header
		// regardless of grid size.
		const string header = "Champion Path";
		Panel panel = new Panel(canvas).Header(header);
		panel.Width = Math.Max(boundary.Width + 4, header.Length + 4);
		return panel;
	}

	// Eater's Orientation.Up increases Y (see Step.cs); Canvas renders row 0 at the top with Y
	// increasing downward. Flipping here keeps "up" visually up instead of the path rendering
	// upside down.
	private static int ToCanvasY(int y, Size boundary) => boundary.Height - 1 - y;

	private static bool InBounds(Point p, Size boundary)
		=> p.X >= 0 && p.X < boundary.Width && p.Y >= 0 && p.Y < boundary.Height;

	private static Color VisitGradient(double t)
	{
		t = Math.Clamp(t, 0d, 1d);
		byte r = (byte)(40 + t * 180);
		byte g = (byte)(70 + t * 140);
		byte b = (byte)(200 - t * 160);
		return new Color(r, g, b);
	}

	/// <summary>Task 15-0038 AC3: renders one row per <see cref="ChampionCard"/>, ordered by problem then pool.</summary>
	public static IRenderable BuildChampionCards(IReadOnlyCollection<ChampionCard> cards)
	{
		var table = new Table().Expand();
		table.AddColumn("Pool");
		table.AddColumn("Hash");
		table.AddColumn("Genes");
		table.AddColumn("Food-Found-Rate");
		table.AddColumn("Energy");
		table.AddColumn("Samples");

		foreach (ChampionCard c in cards.OrderBy(c => c.ProblemId).ThenBy(c => c.PoolIndex))
		{
			table.AddRow(
				new Text($"{c.ProblemId}.{c.PoolIndex}"),
				new Text(ShortHash(c.Hash)),
				new Text(c.GeneCount.ToString("n0")),
				new Text(double.IsNaN(c.FoodFoundRate) ? "n/a" : c.FoodFoundRate.ToString("p1")),
				new Text(double.IsNaN(c.AverageEnergy) ? "n/a" : c.AverageEnergy.ToString("n3")),
				new Text(c.SampleCount.ToString("n0")));
		}

		// Not .Expand()-ed for the same reason BuildPathCanvas's outer panel isn't -- see its
		// comment. The table itself still expands to fill the available width, just not height.
		return new Panel(table).Header("Champion Cards");
	}

	/// <summary>
	/// Task 15-0038 AC4: a bar-style rendering of the best gene count's recent trajectory --
	/// <paramref name="bestGeneCountsOverTime"/> is expected to already be the caller's rolling
	/// "best-so-far value at each improvement" history (oldest first), capped by the caller at
	/// whatever window it wants to keep (task 15-0038 asks for at least the last 10 improvements).
	/// </summary>
	public static IRenderable BuildGeneCountTrend(IReadOnlyList<int> bestGeneCountsOverTime)
	{
		if (bestGeneCountsOverTime.Count == 0)
			return new Panel(new Text("(no perfect scorer yet)")).Header("Gene-Count Trend");

		int max = bestGeneCountsOverTime.Max();
		IEnumerable<IRenderable> rows = bestGeneCountsOverTime.Select(g =>
		{
			int barLength = max <= 0 ? 1 : Math.Max(1, (int)Math.Round(20d * g / max));
			return (IRenderable)new Text($"{g,5:n0} {new string('#', barLength)}");
		});

		// Not .Expand()-ed -- see BuildPathCanvas's comment on why panels meant to stack via
		// Rows() must size to their own content rather than greedily claiming the shared slot.
		return new Panel(new Rows(rows)).Header("Gene-Count Trend");
	}

	/// <summary>
	/// Assembles the three Eater panels in display order for <see cref="EaterConsoleEmitter.BuildExtraPanels"/>
	/// to hand to <see cref="Solve.Experiment.Console.RunnerDisplay.BuildLayout"/>'s <c>extraPanels</c> slot.
	/// </summary>
	public static IReadOnlyList<IRenderable> BuildPanels(
		Size boundary,
		ImmutableArray<Point> trace,
		IReadOnlyCollection<Point> foodExamples,
		Point start,
		IReadOnlyCollection<ChampionCard> cards,
		IReadOnlyList<int> geneCountTrend)
		=>
		[
			BuildPathCanvas(boundary, trace, foodExamples, start),
			BuildChampionCards(cards),
			BuildGeneCountTrend(geneCountTrend),
		];

	private static string ShortHash(string hash)
		=> hash.Length <= 16 ? hash : string.Concat(hash.AsSpan(0, 16), "…");
}
