using Solve;
using System;
using System.Drawing;
using System.Threading.Tasks;

namespace Eater
{
	public class SampleMetricsAsync : IAsyncFitnessResult<EaterMetric, int>
	{
		public SampleMetricsAsync(EaterGenome genome, Point boundary, (Point Start, Point Food) sample)
		{
			Genome = genome;
			GeneCount = new ValueTask<int>(genome.Length);
			Result = new Lazy<Task<(bool Success, int Energy)>>(
				() => Task.Run(() => genome.Try(boundary, sample.Start, sample.Food)));
			Success = new Lazy<ValueTask<int>>(
				() => new ValueTask<int>(Result.Value.ContinueWith(t => t.Result.Success ? 1 : 0)));
			Energy = new Lazy<ValueTask<int>>(
				() => new ValueTask<int>(Result.Value.ContinueWith(t => t.Result.Energy)));
		}

		public readonly EaterGenome Genome;
		readonly ValueTask<int> GeneCount;
		public readonly Lazy<Task<(bool Success, int Energy)>> Result;
		readonly Lazy<ValueTask<int>> Success;
		readonly Lazy<ValueTask<int>> Energy;

		public ValueTask<int> this[EaterMetric index]
		{
			get
			{
				switch (index)
				{
					case EaterMetric.Success:
						return Success.Value;
					case EaterMetric.Energy:
						return Energy.Value;
					case EaterMetric.GeneCount:
						return GeneCount;
					default:
						throw new ArgumentOutOfRangeException(nameof(index), index, "Unknown metric.");
				}
			}
		}
	}
}
