using Solve.Metrics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Eater;

public partial class GenomeFactory(
	CounterRegistry metrics, IEnumerable<Genome>? seeds = null, bool leftTurnDisabled = false)
	: Solve.ReducibleGenomeFactoryBase<Genome>(metrics, seeds)
{
	// ReSharper disable once UnusedParameter.Local
	public GenomeFactory(CounterRegistry metrics, Genome seed, bool leftTurnDisabled = false)
		: this(metrics, seed is null ? default : [seed], leftTurnDisabled)
	{
	}

	/// <summary>
	/// Constructs a factory with an explicit randomness source (see
	/// <see cref="Solve.GenomeFactoryBase{TGenome}.RandomSource"/>). Two factories built with
	/// equally-seeded <see cref="Random"/> instances (e.g. <c>new Random(42)</c> for both) will
	/// generate a bit-identical sequence of genome hashes when driven single-threaded/in a fixed
	/// call order -- this is the reproducibility entry point added by task 10-0002.
	/// </summary>
	public GenomeFactory(CounterRegistry metrics, Random randomSource, IEnumerable<Genome>? seeds = null, bool leftTurnDisabled = false)
		: this(metrics, seeds, leftTurnDisabled)
	{
		RandomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
	}

	public static IEnumerable<string> Random(int moves, int maxMoveLength, bool leftTurnDisabled = false)
	{
		int size = moves * 2 - 1;
		var steps = new StepCount[size];
		var random = new Random();

		while (true)
		{
			for (int i = 0; i < size; i++)
			{
				steps[i] = (i % 2) switch
				{
					0 => new StepCount(Step.Forward, random.Next(maxMoveLength) + 1),
					1 => new StepCount(leftTurnDisabled ? Step.TurnRight : (random.Next(2) == 0 ? Step.TurnRight : Step.TurnLeft)),
					_ => throw new NotImplementedException()
				};
			}

			// Must start and end with foward movements.  Turns are wasted.
			Debug.Assert(steps[0].Step == Step.Forward);
			Debug.Assert(steps[size - 1].Step == Step.Forward);

			string hash = steps.AsSpan().ToGenomeHash();
			yield return hash;
		}
	}

	public readonly ImmutableArray<Step> AvailableSteps = leftTurnDisabled ? Steps.ALL.Where(s => s != Step.TurnLeft).ToImmutableArray() : Steps.ALL;
}
