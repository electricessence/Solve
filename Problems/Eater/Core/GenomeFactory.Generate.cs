using System;
using System.Threading;

namespace Eater;

public partial class GenomeFactory
{
	private int _generatedCount;
	public int GeneratedCount => _generatedCount;

	/*
	 * The goal here is to produce unique eaters with a bounded, gradually
	 * increasing complexity. Randomness plus the factory's registration
	 * dedup provides uniqueness.
	 *
	 * The previous implementation appended to one ever-growing shared chain,
	 * so the Nth generated genome carried ~N²/2 genes; late in a run the
	 * evaluation cost of these monsters starved the entire pipeline.
	 */
	protected override Genome GenerateOneInternal()
	{
		int n = Interlocked.Increment(ref _generatedCount);
		int moves = Math.Min(n, 60);
		int size = moves * 2 - 1;
		bool leftDisabled = !AvailableSteps.Contains(Step.TurnLeft);
		var random = System.Random.Shared;
		var steps = new StepCount[size];
		for (int i = 0; i < size; i++)
		{
			// Must start and end with forward movements. Turns are wasted.
			steps[i] = (i % 2) switch
			{
				0 => new StepCount(Step.Forward, random.Next(10) + 1),
				_ => new StepCount(leftDisabled ? Step.TurnRight : (random.Next(2) == 0 ? Step.TurnRight : Step.TurnLeft))
			};
		}

		return new Genome(steps);
	}
}
