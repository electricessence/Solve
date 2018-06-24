using System;

namespace Eater
{
	public class EaterFitness2 : EaterFitness1
	{
		public EaterFitness2(SampleMetricsCache cache) : base(cache)
		{

		}

		public override double GetValue(int index, int deep)
		{
			var e = Source[index].Span;
			switch (deep)
			{
				case 0:
					return e[0];
				case 1:
					return -e[2];
				case 2:
					return -e[1];
				default:
					throw new ArgumentOutOfRangeException(nameof(deep), deep, "Must be a positive number less than the depth.");
			}
		}

		public ReadOnlySpan<string> Labels
			=> SampleMetricsCache.Labels;

		public ReadOnlySpan<string> Labels
			=> SampleMetricsCache.Labels;
	}
}
