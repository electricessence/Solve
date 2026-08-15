using Eater;
using System.Collections.Immutable;
using System.Drawing;

namespace Solve.Tests;

/// <summary>
/// Task 15-0038 AC1: <see cref="PathTracer"/> is a pure function over a gene sequence, a grid
/// boundary, and a start point -- these tests hand-build small step sequences whose visit order is
/// known ahead of time and assert the exact returned sequence, rather than driving a real genome
/// through a scheme/environment.
/// </summary>
public class PathTracerTests
{
	private static readonly Size Grid10 = new(10, 10);

	[Fact]
	public void Trace_KnownGenome_ProducesExpectedVisitSequence()
	{
		// "3^>3^" expands (Steps.FromGenomeHash's repeat-count syntax) to Forward x3, TurnRight,
		// Forward x3 -- three steps up from (0,0), a right turn, then three steps right.
		var genome = Genome.Parse("3^>3^");

		ImmutableArray<Point> trace = genome.Trace(Grid10, new Point(0, 0));

		Point[] expected =
		[
			new(0, 0), // start
			new(0, 1),
			new(0, 2),
			new(0, 3), // three forwards while facing Up
			new(1, 3),
			new(2, 3),
			new(3, 3), // three forwards while facing Right (after the turn)
		];

		Assert.Equal(expected, trace);
	}

	[Fact]
	public void Trace_TurnsDoNotAppendVisitedCells()
	{
		// Forward, then three turns (no net movement), then one more Forward -- only two cells
		// should ever be recorded as "visited" beyond the start: the turns are orientation-only.
		ImmutableArray<Step> genes = [Step.Forward, Step.TurnRight, Step.TurnRight, Step.TurnLeft, Step.Forward];

		ImmutableArray<Point> trace = PathTracer.Trace(genes, Grid10, new Point(5, 5));

		// Start (5,5) -> Forward Up -> (5,6) -> turn right twice (now facing Down), turn left once
		// (now facing Right) -> Forward Right -> (6,6).
		Point[] expected = [new(5, 5), new(5, 6), new(6, 6)];
		Assert.Equal(expected, trace);
	}

	[Fact]
	public void Trace_EmptyGenes_ReturnsJustTheStart()
	{
		ImmutableArray<Point> trace = PathTracer.Trace(ImmutableArray<Step>.Empty, Grid10, new Point(2, 3));
		Point single = Assert.Single(trace);
		Assert.Equal(new Point(2, 3), single);
	}

	[Fact]
	public void Trace_WallClamping_MatchesStepsForwardSemantics()
	{
		// Reuses the exact same Steps.Forward(Size, Point, Orientation) wall-clamping extension
		// Steps.Try(...) simulates against -- five consecutive Forward genes from one cell short of
		// the top edge should clamp after the first step, recording the same clamped cell again on
		// every subsequent step rather than throwing or wrapping.
		ImmutableArray<Step> genes = [Step.Forward, Step.Forward, Step.Forward, Step.Forward, Step.Forward];
		var start = new Point(4, 8); // boundary.Height == 10, so Y == 9 is the last valid row.

		ImmutableArray<Point> trace = PathTracer.Trace(genes, Grid10, start);

		Point[] expected =
		[
			new(4, 8),
			new(4, 9), // moves once...
			new(4, 9), // ...then clamps for every remaining Forward.
			new(4, 9),
			new(4, 9),
			new(4, 9),
		];
		Assert.Equal(expected, trace);
	}

	[Fact]
	public void Trace_StartOutsideBoundary_Throws()
	{
		ImmutableArray<Step> genes = [Step.Forward];
		Assert.Throws<ArgumentOutOfRangeException>(() => PathTracer.Trace(genes, Grid10, new Point(10, 0)));
		Assert.Throws<ArgumentOutOfRangeException>(() => PathTracer.Trace(genes, Grid10, new Point(0, -1)));
	}
}
