using Solve.Evaluation;
using Solve.Metrics;

using EvaluationRegistry = Open.Evaluation.Registry;

namespace Solve.Tests;

public class NumericFactoryTests
{
	private sealed class ExposedNumericFactory(CounterRegistry metrics)
		: NumericEvalGenomeFactory(metrics)
	{
		public IEnumerable<EvalGenome<double>> GenerateOperatedPublic(ushort paramCount)
			=> GenerateOperated(paramCount);

		public IEnumerable<EvalGenome<double>> GenerateFunctionedPublic(ushort id)
			=> GenerateFunctioned(id);
	}

	private static ExposedNumericFactory CreateFactory()
		=> new(new CounterRegistry());

	[Fact]
	public void GenerateOperated_YieldsArithmeticOperatorGenomes()
	{
		ExposedNumericFactory factory = CreateFactory();
		List<EvalGenome<double>> genomes = [.. factory.GenerateOperatedPublic(2).Take(4)];
		Assert.NotEmpty(genomes);
		Assert.All(genomes, g =>
		{
			Assert.NotNull(g);
			Assert.False(string.IsNullOrEmpty(g.Hash));
		});
	}

	[Fact]
	public void GenerateFunctioned_YieldsOneGenomePerRegistryFunction()
	{
		// Previously the switch inside GenerateFunctioned matched on Exponent.SYMBOL ('^'),
		// which never appears in Registry.Arithmetic.Functions ('²', '/', '√'), so no cases
		// ever matched and the method silently produced nothing -- the square-root and
		// division (and square) seed genomes it was written to produce were dead code.
		ExposedNumericFactory factory = CreateFactory();
		List<EvalGenome<double>> genomes = [.. factory.GenerateFunctionedPublic(0)];

		Assert.NotEmpty(genomes);

		// Every function currently registered in Registry.Arithmetic.Functions ('²', '/', '√')
		// operates on a single child, so GenerateFunctioned should yield exactly one genome
		// per registered function symbol for the given parameter id.
		Assert.Equal(EvaluationRegistry.Arithmetic.Functions.Length, genomes.Count);

		Assert.All(genomes, g =>
		{
			Assert.NotNull(g);
			Assert.False(string.IsNullOrEmpty(g.Hash));
		});

		// Each function should produce a structurally distinct genome (square, reciprocal,
		// square-root of the same parameter are not the same expression).
		Assert.Equal(genomes.Count, genomes.Select(g => g.Hash).Distinct().Count());
	}
}
