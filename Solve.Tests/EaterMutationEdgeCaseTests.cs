using Eater;
using Solve.Metrics;
using System.Collections.Immutable;

namespace Solve.Tests;

/// <summary>
/// Regression coverage for the Debug-configuration landmines fixed in task 10-0007:
/// mutating a minimal (single-gene) genome must never throw and must never yield an
/// empty genome -- removal-heavy mutation paths on very short genomes must fail the
/// mutation attempt (return false / null) rather than construct an invalid Genome.
/// </summary>
public class EaterMutationEdgeCaseTests
{
	[Fact]
	public void SingleGeneGenomeMutationNeverThrowsOrProducesEmptyGenome()
	{
		var metrics = new CounterRegistry();
		var factory = new GenomeFactory(metrics, seeds: null, leftTurnDisabled: true);

		// The smallest possible valid genome: a single Forward step.
		var source = Genome.Parse("^");
		Assert.Equal(1, source.GeneCount);

		int attempts = 0;
		int successes = 0;
		for (int i = 0; i < 200; i++)
		{
			attempts++;

			// No exception should ever escape here -- this is the core regression check.
			bool succeeded = factory.AttemptNewMutation(source, out Genome? mutation);

			if (!succeeded)
			{
				Assert.Null(mutation);
				continue;
			}

			successes++;
			Assert.NotNull(mutation);
			Assert.NotEmpty(mutation.Genes);
			Assert.Equal(Step.Forward, mutation.Genes[0]);
			Assert.Equal(Step.Forward, mutation.Genes[^1]);
			Assert.False(string.IsNullOrEmpty(mutation.Hash));
		}

		Assert.Equal(200, attempts);
	}

	[Fact]
	public void SingleGeneGenomeFreezeIsExercisedDirectlyWithoutThrowing()
	{
		// Genome.Freeze must handle the empty-array edge with a clear ArgumentException,
		// not an IndexOutOfRangeException from indexing an empty array.
		Assert.Throws<ArgumentException>(() => new Genome(ImmutableArray<Step>.Empty));

		// And a legitimate single-gene genome must still construct fine.
		var genome = new Genome(ImmutableArray.Create(Step.Forward));
		Assert.Single(genome.Genes);
	}
}
