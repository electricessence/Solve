using System;

namespace Eater
{
	public class EaterFitness2 : EaterFitness1
	{
		public EaterFitness2(SampleMetricsCache cache) : base(cache)
		{

		}

		public override double GetValue(in int index, in int deep)
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

		static string[] EF2Labels()
		{
			var labels = SampleMetricsCache.Labels;
			var a = new string[labels.Length];
			a[0] = labels[0];
			a[1] = labels[2];
			a[2] = labels[1];
			return a;
		}

		static readonly string[] _labels = EF2Labels();

		public override ReadOnlySpan<string> Labels
			=> _labels.AsSpan();
	}
}
