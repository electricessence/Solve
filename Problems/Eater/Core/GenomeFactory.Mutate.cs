using Open.RandomizationExtensions;
using Solve;
using System;
using System.Collections.Generic;

namespace Eater
{

	public partial class GenomeFactory
	{
		protected override Genome MutateInternal(Genome target)
			=> new(MutateCore(target.Genes).Reduce().TrimTurns());

		private IEnumerable<Step> MutateCore(IReadOnlyList<Step> genes)
		{
			var rand = Randomizer.Random;
			var length = genes.Count;
			var index = rand.Next(length);
			var segments = genes.SpliceAt(index);
			var value = genes[index];

			switch (rand.Next(4))
			{
				// Remove
				case 0:
					// 1, 2, or 3?
					var r = rand.Next(Math.Min(3, segments.Count / 6)) + 1;
					return segments.Remove(r);

				// Move
				case 1:
					return segments
						.Remove(1)
						.SpliceAt(rand.Next(length - 1))
						.Insert(value);

				// Replace
				case 2:
					segments = segments.Remove(1);
					goto case 3;

				// Insert
				case 3:
					return segments.Insert(AvailableSteps.RandomSelectOne());

				default:
					throw new NotSupportedException();
			}
		}



	}
}
