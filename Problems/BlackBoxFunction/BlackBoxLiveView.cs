using Open.DateTimeExtensions;
using Solve;
using Solve.Experiment.Console;
using Spectre.Console;
using Spectre.Console.Rendering;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace BlackBoxFunction;

/// <summary>
/// Pure, side-effect-free builders for BlackBoxFunction's problem-specific live-display content
/// (task 15-0039): the current best expression per pool, Direction/Correlation gauges scaled
/// [0,1], Divergence plus gene-count readouts, and a per-pool convergence banner. Plugs into
/// <see cref="ConsoleEmitterBase{TGenome}.BuildExtraPanels"/> -- the generic extension hook task
/// 15-0037/15-0038 added to <see cref="RunnerDisplay.BuildLayout"/>/<see
/// cref="RunnerDisplay.WritePlainStatus"/> -- see <see cref="EvalConsoleEmitter"/> for the wiring.
/// Kept static and independent of <see cref="EvalConsoleEmitter"/>'s instance state so Solve.Tests
/// can exercise the exact same rendering with hand-built data, the same way
/// <c>SpectreRenderingTests</c> exercises <see cref="RunnerDisplay"/>.
/// </summary>
public static partial class BlackBoxLiveView
{
	/// <summary>
	/// Longest expression text rendered in full before <see cref="BuildExpressionPanel"/> truncates
	/// it with a "…" indicator (AC1). Generous enough that a typical BlackBoxFunction champion never
	/// hits it, tight enough that a pathological expression can't blow out the layout.
	/// </summary>
	public const int MaxExpressionLength = 600;

	/// <summary>
	/// A snapshot of one pool's most recently reported champion fitness. Unlike <see
	/// cref="TopGenomeStat"/> (which only keeps a formatted summary string), this keeps the
	/// individual Direction/Correlation/Divergence metric values so the gauges/readouts below can
	/// render them at full precision -- looked up by <see cref="Metric.Name"/>, not position, since
	/// <c>Problem.Metrics01/02/03</c> permute the same four metrics into different slots per pool.
	/// </summary>
	/// <param name="Direction">Raw Direction metric average, range approximately [-1, 1].</param>
	/// <param name="Correlation">Raw Correlation metric average, range approximately [-1, 1].</param>
	/// <param name="Divergence">
	/// Positive divergence magnitude (smaller is better) -- <c>Problem.cs</c> stores this negated
	/// into the maximized-fitness space, flipped back here for a human-facing readout.
	/// </param>
	/// <param name="Converged">
	/// Whether this pool's fitness currently satisfies <see cref="Fitness.HasConverged"/> -- the
	/// exact same method (and per-metric tolerances) <c>RunnerBase</c>'s own aggregate convergence
	/// check uses. This is a display-only *view* of convergence, not a new definition of it.
	/// </param>
	/// <param name="ConvergedAt">
	/// Elapsed run time the first moment <paramref name="Converged"/> became true for this pool, or
	/// <see langword="null"/> while still searching. Pinned by the caller (not recomputed here) so
	/// it reflects time-*to*-convergence rather than "now" on every subsequent update.
	/// </param>
	public readonly record struct PoolSnapshot(
		double Direction,
		double Correlation,
		double Divergence,
		bool Converged,
		TimeSpan? ConvergedAt);

	/// <summary>
	/// Extracts a <see cref="PoolSnapshot"/> from a just-reported champion <see cref="Fitness"/>.
	/// </summary>
	/// <param name="fitness">The just-updated champion fitness for one pool.</param>
	/// <param name="minConvergenceSamples">
	/// Minimum sample count required before convergence can be declared -- forwarded verbatim to
	/// <see cref="Fitness.HasConverged"/>.
	/// </param>
	/// <param name="elapsed">Elapsed run time at the moment this update was observed.</param>
	/// <param name="previousConvergedAt">
	/// The prior snapshot's <see cref="PoolSnapshot.ConvergedAt"/> (or <see langword="null"/> if
	/// none yet) -- reused as-is once set, so the banner keeps showing the *original*
	/// time-to-convergence rather than drifting forward on every later render tick.
	/// </param>
	public static PoolSnapshot BuildSnapshot(
		Fitness fitness,
		uint minConvergenceSamples,
		TimeSpan elapsed,
		TimeSpan? previousConvergedAt)
	{
		ArgumentNullException.ThrowIfNull(fitness);

		double direction = double.NaN;
		double correlation = double.NaN;
		double divergence = double.NaN;

		foreach ((Metric metric, double value) in fitness.MetricAverages)
		{
			switch (metric.Name)
			{
				case "Direction":
					direction = value;
					break;
				case "Correlation":
					correlation = value;
					break;
				case "Divergence":
					// Problem.cs negates Divergence into the maximized-fitness space (smaller raw
					// divergence -> larger/less-negative stored value) -- flip back for display.
					divergence = -value;
					break;
			}
		}

		bool converged = fitness.HasConverged(minConvergenceSamples);
		TimeSpan? convergedAt = converged ? (previousConvergedAt ?? elapsed) : null;

		return new PoolSnapshot(direction, correlation, divergence, converged, convergedAt);
	}

	/// <summary>
	/// Builds one panel per pool present in <paramref name="topGenomeStats"/>, ordered the same way
	/// <see cref="RunnerDisplay.BuildStatsTable"/> orders its rows (ordinal key order). A pool with
	/// a recorded champion but no matching <paramref name="snapshots"/> entry yet (shouldn't
	/// normally happen -- both are populated from the same update) is skipped rather than rendered
	/// with placeholder metric values.
	/// </summary>
	public static IReadOnlyList<IRenderable> BuildPanels(
		IReadOnlyDictionary<string, TopGenomeStat> topGenomeStats,
		IReadOnlyDictionary<string, PoolSnapshot> snapshots)
	{
		List<IRenderable> panels = [];
		foreach (string key in topGenomeStats.Keys.OrderBy(k => k, StringComparer.Ordinal))
		{
			if (!snapshots.TryGetValue(key, out PoolSnapshot snapshot))
				continue;

			panels.Add(BuildPoolPanel(key, topGenomeStats[key], snapshot));
		}

		return panels;
	}

	/// <summary>
	/// AC1-3: one panel combining the convergence banner, the champion expression, the
	/// Direction/Correlation gauges, and the Divergence/gene-count readouts for a single pool.
	/// </summary>
	public static IRenderable BuildPoolPanel(string key, TopGenomeStat stat, PoolSnapshot snapshot)
	{
		string expression = string.IsNullOrEmpty(stat.GenomeText) ? stat.GenomeHash : stat.GenomeText;

		List<IRenderable> rows =
		[
			BuildConvergenceBanner(snapshot),
			BuildExpressionPanel(expression),
			BuildGaugeRow("Direction", snapshot.Direction),
			BuildGaugeRow("Correlation", snapshot.Correlation),
			BuildReadoutRow("Divergence", snapshot.Divergence, "G6"),
			BuildReadoutRow("Gene-Count", stat.GeneCount, "N0"),
		];

		return new Panel(new Rows(rows)).Header($"BlackBox {key}").Expand();
	}

	/// <summary>
	/// AC1: the champion expression, syntax-tinted (numbers/operators/parens colored -- not
	/// expression-tree pretty-printing, just character-class coloring of the existing hash/string
	/// form per the scope boundary) and wrapped by the containing panel, or truncated with a "…
	/// (truncated, N chars total)" indicator beyond <see cref="MaxExpressionLength"/>.
	/// </summary>
	public static IRenderable BuildExpressionPanel(string expression)
	{
		ArgumentNullException.ThrowIfNull(expression);

		string display = expression.Length > MaxExpressionLength
			? string.Concat(
				expression.AsSpan(0, MaxExpressionLength),
				"… (truncated, ", expression.Length.ToString("n0", CultureInfo.InvariantCulture), " chars total)")
			: expression;

		// Deliberately not a nested Spectre Panel: a Panel with .Expand() placed inside the outer
		// per-pool panel's Rows (itself inside a Layout region that stretches its content to fill
		// the region's full height) claims the *entire* remaining height for itself, pushing every
		// sibling row (gauges/readouts below) out of the rendered output entirely rather than just
		// wrapping tightly around its own content. A labeled Rows/Markup block sizes to content
		// the way the surrounding per-pool Rows expects.
		return new Rows(
			new Markup("[bold]Expression:[/]"),
			new Markup(TintExpression(display)));
	}

	/// <summary>
	/// Applies light syntax coloring to an expression string: numbers in cyan, arithmetic operators
	/// in yellow, parentheses in grey, everything else (parameter letters, unicode exponents, the
	/// truncation indicator) left as plain text. All matched tokens are <see cref="Markup.Escape"/>d
	/// before insertion; BlackBoxFunction's generated expression alphabet (digits, letters,
	/// <c>+-*/^()</c>, superscript exponent digits, whitespace) never contains the <c>[</c>/<c>]</c>
	/// characters Spectre markup treats specially, but escaping stays defensive regardless.
	/// </summary>
	public static string TintExpression(string expression)
		=> ExpressionTokenPattern().Replace(expression, m =>
		{
			string token = m.Value;
			string color = m.Groups["num"].Success
				? "cyan"
				: m.Groups["op"].Success
					? "yellow"
					: "grey"; // paren
			return $"[{color}]{Markup.Escape(token)}[/]";
		});

	/// <summary>
	/// AC2: a [0,1]-scaled block-character gauge plus the raw value at 10 significant digits
	/// (<c>"G10"</c>) for Direction/Correlation. Values outside [0,1] (Correlation/Direction can go
	/// negative for a poorly-fit genome) clamp the *bar* but not the printed number, so the gauge
	/// never looks visually wrong while the exact value stays visible.
	/// </summary>
	public static IRenderable BuildGaugeRow(string label, double rawValue, int width = 24)
	{
		double clamped = double.IsNaN(rawValue) ? 0d : Math.Clamp(rawValue, 0d, 1d);
		int filled = (int)Math.Round(clamped * width, MidpointRounding.AwayFromZero);
		string bar = string.Concat(new string('█', filled), new string('░', width - filled));
		string color = clamped >= 1d ? "green" : clamped >= 0.9d ? "yellow" : "red";
		string precise = rawValue.ToString("G10", CultureInfo.InvariantCulture);

		return new Markup($"{Markup.Escape(label),-12}[{color}]{bar}[/] {Markup.Escape(precise)}");
	}

	/// <summary>AC2: a plain numeric readout (Divergence, Gene-Count) -- no gauge, just the value.</summary>
	public static IRenderable BuildReadoutRow(string label, double value, string format)
		=> new Markup($"{Markup.Escape(label),-12}{Markup.Escape(value.ToString(format, CultureInfo.InvariantCulture))}");

	/// <summary>
	/// AC3: run-state banner -- "Searching" while active, or "Converged" with the elapsed time it
	/// first fired, once <see cref="PoolSnapshot.Converged"/> is true.
	/// </summary>
	public static IRenderable BuildConvergenceBanner(PoolSnapshot snapshot)
		=> snapshot.Converged
			? new Markup($"[green]● Converged[/] in [bold]{Markup.Escape((snapshot.ConvergedAt ?? TimeSpan.Zero).ToStringVerbose())}[/]")
			: new Markup("[yellow]● Searching…[/]");

	// Numbers (including decimals), the four arithmetic operator characters, and parentheses.
	// Everything else (letters, unicode superscript exponents, whitespace) falls through untouched.
	[GeneratedRegex(@"(?<num>-?\d+(\.\d+)?)|(?<op>[+\-*/^])|(?<paren>[()])")]
	private static partial Regex ExpressionTokenPattern();
}
