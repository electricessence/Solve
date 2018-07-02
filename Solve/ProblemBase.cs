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
		protected class Pool : IProblemPool<TGenome>
		{
			public Pool(ushort poolSize, IReadOnlyList<Metric> metrics, Func<TGenome, double[], FitnessContainer> transform)
			{
				Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
				Transform = transform ?? throw new ArgumentNullException(nameof(transform));

				Champions = poolSize == 0 ? null : new RankedPool<TGenome>(poolSize);
			}

			public Func<TGenome, double[], FitnessContainer> Transform { get; }
			public RankedPool<TGenome> Champions { get; }

			class GF
			{
				public GF(TGenome genome, FitnessContainer fitness)
				{
					Genome = genome;
					Fitness = fitness;
				}

				public readonly TGenome Genome;
				public readonly FitnessContainer Fitness;

				public static implicit operator (TGenome Genome, FitnessContainer Fitness) (GF gf)
					=> (gf.Genome, gf.Fitness);
			}

			GF _bestFitness;
			public (TGenome Genome, FitnessContainer Fitness) BestFitness => _bestFitness;

			public IReadOnlyList<Metric> Metrics { get; }

			public bool UpdateBestFitness(TGenome genome, FitnessContainer fitness)
			{
				if (fitness == null) throw new ArgumentNullException(nameof(fitness));
				fitness = fitness.Clone();

				GF contending = null;
				GF defending;
				while ((defending = _bestFitness) == null || fitness.Results.Average.IsGreaterThan(defending.Fitness.Results.Average))
				{
					contending = contending ?? new GF(genome, fitness);
					if (Interlocked.CompareExchange(ref _bestFitness, contending, defending) == defending)
						return true;
				}
				return false;
			}
		}

		static int ProblemCount = 0;
		public int ID { get; } = Interlocked.Increment(ref ProblemCount);

		public IReadOnlyList<IProblemPool<TGenome>> Pools { get; }

		long _testCount = 0;
		public long TestCount => _testCount;

		protected ProblemBase(IEnumerable<(IReadOnlyList<Metric> Metrics, Func<TGenome, double[], FitnessContainer> Transform)> fitnessTransators, ushort championPoolSize = 0)
		{
			Pools = fitnessTransators?.Select(t => new Pool(championPoolSize, t.Metrics, t.Transform)).ToList().AsReadOnly()
				?? throw new ArgumentNullException(nameof(fitnessTransators));
		}

		protected ProblemBase(ushort championPoolSize, params (IReadOnlyList<Metric> Metrics, Func<TGenome, double[], FitnessContainer> Transform)[] fitnessTranslators)
			: this(fitnessTranslators, championPoolSize) { }

		protected ProblemBase(params (IReadOnlyList<Metric> Metrics, Func<TGenome, double[], FitnessContainer> Transform)[] fitnessTranslators)
			: this(fitnessTranslators, 0) { }


		protected abstract double[] ProcessSampleMetrics(TGenome g, long sampleId);

		public IEnumerable<FitnessContainer> ProcessSample(TGenome g, long sampleId)
		{
			var metrics = ProcessSampleMetrics(g, sampleId);
			Interlocked.Increment(ref _testCount);
			return Pools.Select(p => p.Transform(g, metrics));
		}

		protected virtual Task<double[]> ProcessSampleMetricsAsync(TGenome g, long sampleId)
			=> Task.FromResult(ProcessSampleMetrics(g, sampleId));

		public async Task<IEnumerable<FitnessContainer>> ProcessSampleAsync(TGenome g, long sampleId = 0)
		{
			var metrics = await ProcessSampleMetricsAsync(g, sampleId);
			Interlocked.Increment(ref _testCount);
			return Pools.Select(p => p.Transform(g, metrics));
		}

	}
}
