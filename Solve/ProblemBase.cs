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

		protected ProblemBase(ushort championPoolSize, params Func<TGenome, double[], double[]>[] fitnessTranslators) : this(fitnessTranslators, championPoolSize)
		{

		}

		protected ProblemBase(params Func<TGenome, double[], double[]>[] fitnessTranslators) : this(fitnessTranslators, 0)
		{

		}

		protected readonly IReadOnlyList<Func<TGenome, double[], double[]>> FitnessTranslators;

		protected abstract double[] ProcessSampleMetrics(TGenome g, long sampleId);

		public IEnumerable<double[]> ProcessSample(TGenome g, long sampleId)
		{
			var metrics = ProcessSampleMetrics(g, sampleId);
			Interlocked.Increment(ref _testCount);
			return FitnessTranslators.Select(t => t(g, metrics));
		}

		protected virtual Task<double[]> ProcessSampleMetricsAsync(TGenome g, long sampleId)
			=> Task.FromResult(ProcessSampleMetrics(g, sampleId));

		public async Task<IEnumerable<double[]>> ProcessSampleAsync(TGenome g, long sampleId = 0)
		{
			var metrics = await ProcessSampleMetricsAsync(g, sampleId);
			Interlocked.Increment(ref _testCount);
			return FitnessTranslators.Select(t => t(g, metrics));
		}

		public abstract IReadOnlyList<string> FitnessLabels { get; }


	}
}
