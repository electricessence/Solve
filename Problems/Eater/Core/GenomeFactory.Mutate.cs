using Open.RandomizationExtensions;
using Solve;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Eater;

public partial class GenomeFactory
{
	protected override Genome? MutateInternal(Genome target)
	{
		// Removal-heavy mutation paths (e.g. Case 0 on a 1-gene source) can legitimately
		// reduce the gene sequence to nothing. Constructing a Genome from an empty sequence
		// is invalid, so report mutation failure (null) instead of letting Genome's Freeze
		// throw on the empty array.
		ImmutableArray<Step> steps = MutateCore(target.Genes).Reduce().TrimTurns().ToImmutableArray();
		return steps.Length == 0 ? null : new Genome(steps);
	}

	private IEnumerable<Step> MutateCore(IReadOnlyList<Step> genes)
	{
		Random rand = RandomSource; // 10-0002: injected (seedable) source instead of the previous unseedable ambient default.
		int length = genes.Count;
		int index = rand.Next(length);
		var segments = genes.SpliceAt(index);
		var value = genes[index];

		switch (rand.Next(4))
		{
			// Remove
			case 0:
				// 1, 2, or 3?
				int r = rand.Next(Math.Min(3, segments.Count / 6)) + 1;
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
				return segments.Insert(AvailableSteps.RandomSelectOne(rand));

			default:
				throw new NotSupportedException();
		}
	}
}
