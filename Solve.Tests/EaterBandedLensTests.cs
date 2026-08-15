using Eater;
using Solve;

namespace Solve.Tests;

public class EaterBandedLensTests
{
	// Mirrors EaterTests.IdealSeed's approach of using a plain "<n>^" hash: since GeneCount is
	// derived purely from Genes.Length, a straight run of n forward steps is sufficient to
	// produce a genome with an exact, arbitrary gene count for these ranking tests -- no actual
	// grid traversal is exercised.
	private static Genome GenomeWithGeneCount(int count)
		=> Genome.Parse($"{count}^");

	[Theory]
	[InlineData(0.97, 150)]
	[InlineData(0.99, 1)]
	[InlineData(0.95, 42)]
	public void Band_TiesRateWithinTopBand_ToExactOne(double rate, int band100)
	{
		// band100 is unused as data beyond forcing distinct theory cases; kept simple/explicit.
		_ = band100;
		Assert.Equal(Problem.BandFoodFoundRate(1.0), Problem.BandFoodFoundRate(rate));
	}

	[Fact]
	public void Band_ExactlyOne_MapsToTopBand_NotBelowNinetyFivesBandTop()
	{
		// Regression guard for the floating-point edge described in the task: naive
		// Math.Floor(r / band) * band can misband values that are exact multiples of `band`
		// (e.g. 0.95 can divide down to 18.999999999999996 and floor to 18 instead of 19).
		// A value of exactly 1.0 must land in the same top band as 0.95, never lower.
		double topBand = Problem.BandFoodFoundRate(1.0);
		double bandAtNinetyFive = Problem.BandFoodFoundRate(0.95);

		Assert.Equal(bandAtNinetyFive, topBand);
		Assert.True(topBand >= 0.9499, $"Expected top band >= ~0.95, got {topBand:R}.");
	}

	[Theory]
	[InlineData(0.94)]
	[InlineData(0.89)]
	[InlineData(0.50)]
	[InlineData(0.0)]
	public void Band_BelowTopBand_IsStrictlyLower(double rate)
	{
		double topBand = Problem.BandFoodFoundRate(1.0);
		double banded = Problem.BandFoodFoundRate(rate);
		Assert.True(banded < topBand, $"Expected {rate} (banded to {banded:R}) to be below the top band ({topBand:R}).");
	}

	[Fact]
	public void Band_CustomGranularity_IsRespected()
	{
		// With a coarser 0.1 band, 0.83 and 0.89 both fall in the [0.8, 0.9) band.
		Assert.Equal(Problem.BandFoodFoundRate(0.83, 0.1), Problem.BandFoodFoundRate(0.89, 0.1));
		// ...but 0.79 falls one band lower.
		Assert.True(Problem.BandFoodFoundRate(0.79, 0.1) < Problem.BandFoodFoundRate(0.83, 0.1));
	}

	/// <summary>
	/// Core acceptance scenario: under the banded lens, a genome with slightly-below-perfect
	/// coverage but far fewer genes (0.97, 150 genes) must outrank a pristine-but-bloated genome
	/// (1.0, 300 genes), because both tie on the banded Food-Found-Rate key and gene count then
	/// decides. Under the strict lens (exact Food-Found-Rate first), the ranking is reversed.
	/// </summary>
	[Fact]
	public void BandedLens_RanksSmallerNearPerfectGenome_AboveLargerPerfectGenome_WhileStrictLensReversesIt()
	{
		var small = GenomeWithGeneCount(150);
		var large = GenomeWithGeneCount(300);

		// values array shape: [foodFoundRate, averageEnergy, averageWasted] (see
		// Problem.ProcessSampleMetrics), consumed by every pool's Transform.
		double[] smallSampleValues = [0.97, 0, 0];
		double[] largeSampleValues = [1.0, 0, 0];

		var problem = Eater.Problem.CreateFitnessSecondaryWithBandedLens();

		var strictPool = problem.Pools[0]; // MetricsSecondary01: [FoodFoundRate, AverageEnergy, GeneCount]
		var bandedPool = problem.Pools[2]; // MetricsSecondaryBanded: [BandedFoodFoundRate, GeneCount, AverageEnergy]

		Assert.Equal("Food-Found-Rate", strictPool.Metrics[0].Name);
		Assert.Equal("Food-Found-Rate (Banded)", bandedPool.Metrics[0].Name);

		Fitness smallStrict = strictPool.Transform(small, smallSampleValues);
		Fitness largeStrict = strictPool.Transform(large, largeSampleValues);
		Assert.True(largeStrict.IsSuperiorTo(smallStrict),
			"Strict lens must rank the exact-1.0/larger genome above the 0.97/smaller genome.");

		Fitness smallBanded = bandedPool.Transform(small, smallSampleValues);
		Fitness largeBanded = bandedPool.Transform(large, largeSampleValues);
		Assert.True(smallBanded.IsSuperiorTo(largeBanded),
			"Banded lens must rank the smaller near-perfect genome above the larger pristine genome.");
	}

	[Fact]
	public void CreateFitnessSecondaryWithBandedLens_AddsThirdPool_AlongsideExistingTwo()
	{
		var withBand = Eater.Problem.CreateFitnessSecondaryWithBandedLens();
		var withoutBand = Eater.Problem.CreateFitnessSecondary();

		Assert.Equal(3, withBand.Pools.Count);
		Assert.Equal(2, withoutBand.Pools.Count);

		// The first two pools must match the existing secondary pools' metric shape exactly.
		for (int i = 0; i < 2; i++)
		{
			Assert.Equal(withoutBand.Pools[i].Metrics, withBand.Pools[i].Metrics);
		}
	}
}
