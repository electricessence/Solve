using App.Metrics.Counter;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Eater;

public partial class GenomeFactory : Solve.ReducibleGenomeFactoryBase<Genome>
{
	public GenomeFactory(IProvideCounterMetrics metrics, IEnumerable<Genome>? seeds = null, bool leftTurnDisabled = false)
		: base(metrics, seeds) => AvailableSteps = leftTurnDisabled ? Steps.ALL.Where(s => s != Step.TurnLeft).ToImmutableArray() : Steps.ALL;

	// ReSharper disable once UnusedParameter.Local
	public GenomeFactory(IProvideCounterMetrics metrics, Genome seed, bool leftTurnDisabled = false)
		: this(metrics, seed is null ? default(IEnumerable<Genome>) : new[] { seed }, leftTurnDisabled)
	{
	}

	public static IEnumerable<string> Random(int moves, int maxMoveLength, bool leftTurnDisabled = false)
	{
		var size = moves * 2 - 1;
		var steps = new StepCount[size];
		var random = new Random();

		while (true)
		{
			for (var i = 0; i < size; i++)
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

			var hash = steps.AsSpan().ToGenomeHash();
			yield return hash;
		}
	}

	public readonly ImmutableArray<Step> AvailableSteps;
}
