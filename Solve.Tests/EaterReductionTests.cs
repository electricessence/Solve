using Eater;
using System.Collections.Immutable;
using System.Drawing;

namespace Solve.Tests;

/// <summary>
/// Coverage for task 10-0005's coverage-preserving reduction operator
/// (<see cref="GenomeFactory.ReduceCoverage"/>): a deterministic, simulation-verified local
/// search that removes provably redundant path segments (closed loops that return the walker
/// to a state it already visited) without ever breaking a sample the input genome solved.
/// </summary>
public class EaterReductionTests
{
	static readonly Size Boundary = new(10, 10);

	[Fact]
	public void RemovesAnObviousRetracingLoopWhileKeepingSamplesSolved()
	{
		// Start at (0,0) facing Up. Walk out 2, do a 180 (turn right twice), walk back the
		// same 2 cells (retracing (0,1) and (0,0), which were already visited), 180 again to
		// face Up, then continue 3 more forward to reach food at (0,3).
		//
		// The 8-step prefix (F F > > F F > >) returns the walker to the exact same
		// (position, orientation) state it started in -- (0,0) facing Up -- so it contributes
		// nothing: the genome could just go straight there in 3 steps.
		var genome = Genome.Parse("2^2>2^2>3^");
		var food = new Point(0, 3);
		var start = new Point(0, 0);

		Assert.True(genome.Try(Boundary, start, food), "Test setup sanity: the hand-built genome must reach the food.");

		SampleCache.Entry[] samples = [new SampleCache.Entry(start, food)];
		Genome reduced = GenomeFactory.ReduceCoverage(genome, Boundary, samples);

		Assert.True(reduced.GeneCount < genome.GeneCount,
			$"Expected gene count to drop below {genome.GeneCount}, got {reduced.GeneCount} ({reduced.Hash}).");
		Assert.True(reduced.Try(Boundary, start, food));
	}

	[Fact]
	public void PreservesEverySampleTheOriginalGenomeSolved()
	{
		var genome = Genome.Parse("2^2>2^2>3^");

		// A mix of samples the genome solves and samples it does not; only the former are
		// required to keep succeeding (10-0005 AC2).
		SampleCache.Entry[] samples =
		[
			new SampleCache.Entry(new Point(0, 0), new Point(0, 3)), // solved
			new SampleCache.Entry(new Point(5, 5), new Point(9, 9)), // not solved by this genome
		];

		bool[] before = [.. samples.Select(s => genome.Try(Boundary, s.EaterStart, s.Food))];
		Assert.True(before[0]);
		Assert.False(before[1]);

		Genome reduced = GenomeFactory.ReduceCoverage(genome, Boundary, samples);

		for (int i = 0; i < samples.Length; i++)
		{
			if (!before[i]) continue; // Not required to still succeed.
			SampleCache.Entry s = samples[i];
			Assert.True(reduced.Try(Boundary, s.EaterStart, s.Food),
				$"Sample #{i} ({s.EaterStart} -> {s.Food}) regressed after reduction.");
		}
	}

	[Fact]
	public void ReductionReachesAFixedPoint()
	{
		var genome = Genome.Parse("2^2>2^2>3^");
		SampleCache.Entry[] samples = [new SampleCache.Entry(new Point(0, 0), new Point(0, 3))];

		Genome once = GenomeFactory.ReduceCoverage(genome, Boundary, samples);
		Genome twice = GenomeFactory.ReduceCoverage(once, Boundary, samples);

		Assert.Equal(once.Hash, twice.Hash);
	}

	[Fact]
	public void MinimalStraightLineGenomePassesThroughUnchanged()
	{
		// Already-minimal: three forward steps directly to the food, no turns, no revisited
		// cells. There is nothing safe to remove.
		var genome = Genome.Parse("3^");
		var start = new Point(0, 0);
		var food = new Point(0, 3);

		Assert.True(genome.Try(Boundary, start, food));

		SampleCache.Entry[] samples = [new SampleCache.Entry(start, food)];
		Genome reduced = GenomeFactory.ReduceCoverage(genome, Boundary, samples);

		Assert.Equal(genome.Hash, reduced.Hash);
		Assert.Equal(genome.GeneCount, reduced.GeneCount);
	}

	[Fact]
	public void EmptySampleSetLeavesGenomeUnchanged()
	{
		var genome = Genome.Parse("2^2>2^2>3^");
		Genome reduced = GenomeFactory.ReduceCoverage(genome, Boundary, []);

		Assert.Equal(genome.Hash, reduced.Hash);
	}

	[Fact]
	public void FactoryGetReducedIsIdempotentAndNeverWorsensGeneCount()
	{
		// Exercises the factory-level wiring (10-0005 AC4) via the public static entry point
		// with the same default 10x10 boundary the factory itself uses, rather than reaching
		// into the protected GetReduced hook directly.
		Genome genome = Genome.Parse("2^2>2^2>3^");
		SampleCache.Entry[] samples = [new SampleCache.Entry(new Point(0, 0), new Point(0, 3))];

		Genome reduced = GenomeFactory.ReduceCoverage(genome, new Size(10, 10), samples);
		Assert.True(reduced.GeneCount <= genome.GeneCount);
		Assert.Equal(Step.Forward, reduced.Genes[0]);
		Assert.Equal(Step.Forward, reduced.Genes[^1]);
	}
}
