using Solve.Evaluation;
using Solve.Metrics;

namespace Solve.Tests;

public class BooleanFactoryTests
{
	private sealed class ExposedBooleanFactory(CounterRegistry metrics)
		: BooleanEvalGenomeFactory(metrics)
	{
		public IEnumerable<EvalGenome<bool>> GenerateOperatedPublic(ushort paramCount)
			=> GenerateOperated(paramCount);

		public IEnumerable<EvalGenome<bool>> GenerateFunctionedPublic(ushort id)
			=> GenerateFunctioned(id);
	}

	private static ExposedBooleanFactory CreateFactory()
		=> new(new CounterRegistry());

	[Fact]
	public void GenerateOperated_YieldsBooleanOperatorGenomes()
	{
		// Previously threw ArgumentException: arithmetic operators ('+','*') were passed
		// to the boolean registry (valid set: '&','|').
		ExposedBooleanFactory factory = CreateFactory();
		List<EvalGenome<bool>> genomes = [.. factory.GenerateOperatedPublic(2).Take(4)];
		Assert.NotEmpty(genomes);
		Assert.All(genomes, g =>
		{
			Assert.NotNull(g);
			Assert.False(string.IsNullOrEmpty(g.Hash));
		});
	}

	[Fact]
	public void GenerateFunctioned_YieldsNot()
	{
		ExposedBooleanFactory factory = CreateFactory();
		List<EvalGenome<bool>> genomes = [.. factory.GenerateFunctionedPublic(0)];
		Assert.Single(genomes);
		Assert.False(string.IsNullOrEmpty(genomes[0].Hash));
	}

	[Fact]
	public async Task Mutation_ReturnsPromptlyWithoutMutating()
	{
		// Previously an unconditional infinite loop hung the calling thread forever.
		ExposedBooleanFactory factory = CreateFactory();
		EvalGenome<bool> genome = factory.GenerateOperatedPublic(2).First();

		var task = Task.Run(() => factory.AttemptNewMutation(genome, out EvalGenome<bool>? _));
		Task completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2)));
		Assert.Same(task, completed);
		Assert.False(await task, "Boolean mutation is unimplemented and must report failure.");
	}
}
