using Open.RandomizationExtensions;
using System.Collections.Generic;

namespace Eater
{
	public partial class GenomeFactory
	{
		public int GeneratedCount { get; private set; }

		readonly LinkedList<StepCount> _lastGenerated = new LinkedList<StepCount>();

		/*
         * The goal here is to produce unique eaters.
         */
		protected override Genome GenerateOneInternal()
		{
			lock (_lastGenerated)
			{
				var count = GeneratedCount;
				if (count == 0)
				{
					_lastGenerated.AddLast(Step.Forward);
				}
				else
				{
					_lastGenerated.AddLast(Step.TurnRight);
					_lastGenerated.AddLast(StepCount.Forward(Randomizer.Random.Next(count * 2) + 1));
				}

				++GeneratedCount;
				return new Genome(_lastGenerated);
			}
		}
	}
}
