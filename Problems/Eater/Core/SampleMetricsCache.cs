using Solve.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Eater
{
	public class SampleMetricsCache : AutoList<ReadOnlyMemory<int>>
	{
		public SampleMetricsCache(
			EaterGenome genome,
			ReadOnlyXY<int> boundary,
			IReadOnlyList<Sample<int>> source)
			: base(source.Count, i =>
			{
				var (Start, Food) = source[i];
				var (Success, Energy) = genome.Try(boundary, Start, Food);
				return new int[] { Success ? 1 : 0, Energy, genome.Length };
			})
		{
			if (genome == null) throw new ArgumentNullException(nameof(genome));
			if (source == null) throw new ArgumentNullException(nameof(source));
			Contract.EndContractBlock();
		}

		static readonly string[] _labels = new string[] {
			"Food-Found-Rate {0:p}",
			"Average-Energy {0:n3}",
			"Gene-Count {0:n0}"
		};

		public static ReadOnlySpan<string> Labels => _labels.AsSpan();
	}
}
