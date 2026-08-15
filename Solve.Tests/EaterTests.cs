using Eater;
using Solve.Metrics;
using System.Drawing;
using System.Text;

namespace Solve.Tests;

public class EaterTests
{
	/// <summary>Mirrors Eater.Console Runner.GenerateIdealSeed: a spiral sweep guaranteed to cover the grid.</summary>
	private static string IdealSeed(int size)
	{
		var sb = new StringBuilder();
		int s = size - 1;
		sb.Append(s).Append('^');
		for (int i = 0; i < 2; i++)
			sb.Append('>').Append(s).Append('^');
		for (; s > 0; s--)
			sb.Append('>').Append(s).Append('^').Append('>').Append(s).Append('^');
		return sb.ToString();
	}

	[Fact]
	public void GenomeHashRoundtrip()
	{
		var genome = Genome.Parse("4^>2^");
		Assert.Equal(genome.Hash, Genome.Parse(genome.Hash).Hash);
		Assert.Equal(4 + 1 + 2, genome.GeneCount);
	}

	[Fact]
	public void IdealSeedFindsFoodFromAnywhere()
	{
		var genome = Genome.Parse(IdealSeed(10));
		var boundary = new Size(10, 10);
		Assert.True(genome.Try(boundary, new Point(0, 0), new Point(5, 5)));
		Assert.True(genome.Try(boundary, new Point(9, 9), new Point(2, 7)));
	}

	[Fact]
	public void SingleStepCannotReachDistantFood()
	{
		var genome = Genome.Parse("1^");
		Assert.False(genome.Try(new Size(10, 10), new Point(5, 5), new Point(0, 0)));
	}

	[Fact]
	public void GeneratedGenomesAreBounded()
	{
		var metrics = new CounterRegistry();
		var factory = new GenomeFactory(metrics, seeds: null, leftTurnDisabled: true);

		var hashes = new HashSet<string>();
		int produced = 0;
		for (int i = 0; i < 500; i++)
		{
			if (!factory.TryGenerateNew(out Genome? genome))
				continue;

			produced++;
			// Closed-form bound: moves caps at 60 ⇒ ≤ 60×10 forwards + 59 turns.
			Assert.InRange(genome.GeneCount, 1, 700);
			Assert.Equal(Step.Forward, genome.Genes[0]);
			Assert.Equal(Step.Forward, genome.Genes[^1]);
			hashes.Add(genome.Hash);
		}

		Assert.True(hashes.Count >= 100, $"Only {hashes.Count} distinct genomes from {produced} produced.");
	}

	[Fact]
	public void MutationsProduceValidGenomes()
	{
		var metrics = new CounterRegistry();
		var factory = new GenomeFactory(metrics, seeds: null, leftTurnDisabled: true);

		// Decent-sized random sources: tiny genomes can legitimately mutate to nothing.
		Genome[] sources = [.. GenomeFactory.Random(10, 20, leftTurnDisabled: true)
			.Take(20)
			.Select(Genome.Parse)];

		int successes = 0;
		for (int i = 0; i < 200; i++)
		{
			Genome source = sources[i % sources.Length];
			if (!factory.AttemptNewMutation(source, out Genome? mutation))
				continue;

			successes++;
			Assert.NotNull(mutation);
			Assert.NotEmpty(mutation.Genes);
			Assert.Equal(Step.Forward, mutation.Genes[0]);
			Assert.Equal(Step.Forward, mutation.Genes[^1]);
			Assert.False(string.IsNullOrEmpty(mutation.Hash));
		}

		// The overwhelming majority of attempts on 20+ gene genomes must succeed.
		Assert.True(successes > 100, $"Only {successes}/200 mutations succeeded.");
	}
}
