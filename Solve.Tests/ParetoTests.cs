using Open.Collections;
using Solve;
using System.Collections.Immutable;

namespace Solve.Tests;

public class ParetoTests
{
	private static List<string> Filter(Dictionary<string, ImmutableArray<double>> scores, IEnumerable<string>? source = null)
	{
		using ArrayPoolSegment<(string Value, ImmutableArray<double> Score)> result
			= Pareto.Filter(source ?? scores.Keys, StringComparer.Ordinal, k => scores[k]);
		return [.. result.Segment.Select(e => e.Value).Order()];
	}

	[Fact]
	public void DominatedElementIsRemoved()
	{
		List<string> front = Filter(new()
		{
			["A"] = [1, 1],
			["B"] = [2, 2],
		});
		Assert.Equal(["B"], front);
	}

	[Fact]
	public void NonDominatedSetIsKept()
	{
		List<string> front = Filter(new()
		{
			["A"] = [2, 1],
			["B"] = [1, 2],
			["C"] = [0, 0],
		});
		Assert.Equal(["A", "B"], front);
	}

	[Fact]
	public void DuplicateKeysAreDeduplicated()
	{
		Dictionary<string, ImmutableArray<double>> scores = new()
		{
			["A"] = [2, 1],
			["B"] = [1, 2],
		};
		List<string> front = Filter(scores, ["A", "A", "B", "A"]);
		Assert.Equal(["A", "B"], front);
	}

	[Fact]
	public void NaNNeverDominates()
	{
		// B beats A in dim0 (non-NaN beats NaN) but loses dim1 → neither dominates.
		List<string> front = Filter(new()
		{
			["A"] = [double.NaN, 5],
			["B"] = [0, 0],
		});
		Assert.Equal(["A", "B"], front);
	}

	[Fact]
	public void AllNaNIsDominatedByAnyRealScore()
	{
		List<string> front = Filter(new()
		{
			["A"] = [double.NaN, double.NaN],
			["B"] = [0, 0],
		});
		Assert.Equal(["B"], front);
	}

	[Fact]
	public void EqualScoresAllSurvive()
	{
		List<string> front = Filter(new()
		{
			["A"] = [1, 1],
			["B"] = [1, 1],
			["C"] = [1, 1],
		});
		Assert.Equal(["A", "B", "C"], front);
	}

	[Fact]
	public void SingleElementSurvives()
	{
		List<string> front = Filter(new() { ["A"] = [1, 2, 3] });
		Assert.Equal(["A"], front);
	}

	[Fact]
	public void ChainOfDominance_OnlyTopSurvives()
	{
		List<string> front = Filter(new()
		{
			["A"] = [3, 3],
			["B"] = [2, 2],
			["C"] = [1, 1],
		});
		Assert.Equal(["A"], front);
	}
}
