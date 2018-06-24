using Solve;
using System;
using System.Collections.Generic;

namespace Eater
{
	public class EaterFitness1 : SampleFitnessCollectionBase<int, double>
	{
		public EaterFitness1(IReadOnlyList<ReadOnlyMemory<int>> source) : base(source, 3)
		{
			_source = source;
		}

		readonly IReadOnlyList<ReadOnlyMemory<int>> _source;

		public ReadOnlySpan<string> Labels
			=> SampleMetricsCache.Labels;

		public override double GetValue(in int index, in int deep)
		{
			var e = _source[index].Span;
			switch (deep)
			{
				case 0:
					return +e[0];
				case 1:
					return -e[1];
				case 2:
					return -e[2];
				default:
					throw new ArgumentOutOfRangeException(nameof(deep), deep,
						"Must be a positive number less than the depth.");
			}
		}
	}
}
