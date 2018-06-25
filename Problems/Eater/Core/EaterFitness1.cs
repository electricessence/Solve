using Solve;
using System;

namespace Eater
{
	public class EaterFitness1 : SampleFitnessCollectionBase
	{
		public EaterFitness1(SampleMetricsCache source) : base(source, 3)
		{
		}

		public override ReadOnlySpan<string> Labels
			=> SampleMetricsCache.Labels;

		public override double GetValue(in int index, in int deep)
		{
			var e = Source[index].Span;
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
