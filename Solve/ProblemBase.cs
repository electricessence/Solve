using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve
{
	public abstract class ProblemBase<TGenome> : IProblem<TGenome>
		where TGenome : class, IGenome
	{

		public RankedPool<TGenome> ChampionPool { get; }

		static int ProblemCount = 0;
		public int ID { get; } = Interlocked.Increment(ref ProblemCount);

		long _testCount = 0;
		public long TestCount => _testCount;

		protected ProblemBase(IEnumerable<Func<TGenome, double[], double[]>> fitnessTransators, ushort championPoolSize = 0)
		{
			FitnessTranslators = fitnessTransators?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(fitnessTransators));
			if (championPoolSize != 0)
				ChampionPool = new RankedPool<TGenome>(championPoolSize);
		}

		protected readonly IReadOnlyList<Func<TGenome, double[], double[]>> FitnessTranslators;

		protected abstract double[] ProcessSampleMetricsInternal(TGenome g, long sampleId);

		public double[] ProcessSampleMetrics(TGenome g, long sampleId = 0)
		{
			try
			{
				return ProcessSampleMetricsInternal(g, sampleId);
			}
			finally
			{
				Interlocked.Increment(ref _testCount);
			}
		}

		protected virtual Task<double[]> ProcessSampleMetricsAsyncInternal(TGenome g, long sampleId)
			=> Task.FromResult(ProcessSampleMetricsInternal(g, sampleId));

		public async Task<double[]> ProcessSampleMetricsAsync(TGenome g, long sampleId = 0)
		{
			try
			{
				return await ProcessSampleMetricsAsyncInternal(g, sampleId);
			}
			finally
			{
				Interlocked.Increment(ref _testCount);
			}
		}

		public abstract IReadOnlyList<string> FitnessLabels { get; }


	}
}
