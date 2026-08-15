using System;
using System.Collections.Immutable;
using System.Drawing;

namespace Eater;

/// <summary>
/// Task 15-0038 AC1: a pure path-trace helper that computes the ordered visited-cell sequence a
/// genome produces from a given start point on a given grid boundary. Reuses exactly the same
/// wall-clamping movement semantics <see cref="Steps.Try(System.Collections.Generic.IEnumerable{Step}, Size, Point, Point, out int, out int)"/>
/// applies (via the same <see cref="Steps.Forward(Size, Point, Orientation)"/> extension) so the
/// rendered path always matches what a real evaluation run would have walked.
/// </summary>
/// <remarks>
/// Deliberately kept in its own file rather than added to <c>Step.cs</c> or <c>Genome.cs</c> --
/// both of those files are under concurrent edit by other tasks sharing this codebase. This type
/// has no dependency on <c>Problem.cs</c> either (also under concurrent edit); it only needs a
/// gene sequence, a boundary, and a start point, all of which its caller already has.
/// </remarks>
public static class PathTracer
{
	/// <summary>
	/// Walks <paramref name="genes"/> from <paramref name="start"/> on a grid of size
	/// <paramref name="boundary"/>, recording the cell landed on after every
	/// <see cref="Step.Forward"/> gene. <see cref="Step.TurnLeft"/>/<see cref="Step.TurnRight"/>
	/// genes change orientation only and are not recorded as separate visits. The returned
	/// sequence's first element is always <paramref name="start"/> itself (visit order 0), even if
	/// <paramref name="genes"/> is empty or never actually moves off the start cell (e.g. it turns
	/// in place, or every forward step is wall-clamped back onto the same cell).
	/// </summary>
	/// <param name="genes">The step sequence to walk -- typically a <see cref="Genome"/>'s <see cref="Genome.Genes"/>.</param>
	/// <param name="boundary">The grid's size; movement is clamped at its edges exactly as <see cref="Steps.Forward(Size, Point, Orientation)"/> clamps it.</param>
	/// <param name="start">The starting cell. Must lie within <paramref name="boundary"/>.</param>
	/// <returns>The ordered visited-cell sequence, starting with <paramref name="start"/>.</returns>
	public static ImmutableArray<Point> Trace(ImmutableArray<Step> genes, Size boundary, Point start)
	{
		if (boundary.Width < 1 || boundary.Height < 1)
			throw new ArgumentOutOfRangeException(nameof(boundary), boundary, "Must be at least 1x1.");

		if (start.X < 0 || start.X >= boundary.Width || start.Y < 0 || start.Y >= boundary.Height)
			throw new ArgumentOutOfRangeException(nameof(start), start, "Start is outside the grid boundary.");

		ImmutableArray<Point>.Builder visited = ImmutableArray.CreateBuilder<Point>(genes.Length + 1);
		Point current = start;
		Orientation orientation = Orientation.Up;
		visited.Add(current);

		foreach (Step step in genes)
		{
			switch (step)
			{
				case Step.Forward:
					current = boundary.Forward(current, orientation);
					visited.Add(current);
					break;

				case Step.TurnLeft:
					orientation = orientation.TurnLeft();
					break;

				case Step.TurnRight:
					orientation = orientation.TurnRight();
					break;
			}
		}

		return visited.ToImmutable();
	}

	/// <inheritdoc cref="Trace(ImmutableArray{Step}, Size, Point)"/>
	public static ImmutableArray<Point> Trace(this Genome genome, Size boundary, Point start)
		=> Trace(genome.Genes, boundary, start);
}
