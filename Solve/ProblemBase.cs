using Open.Memory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
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
			public Pool(ushort poolSize, in ReadOnlyMemory<Metric> metrics, Func<TGenome, double[], Fitness> transform)
			{
				Metrics = metrics;
				Transform = transform ?? throw new ArgumentNullException(nameof(transform));

				Champions = poolSize == 0 ? null : new RankedPool<TGenome>(poolSize);
			}

			public ReadOnlyMemory<Metric> Metrics { get; }

			public Func<TGenome, double[], Fitness> Transform { get; }

			public RankedPool<TGenome> Champions { get; }

			class GF
			{
				public GF(TGenome genome, Fitness fitness)
				{
					Debug.Assert(genome != null);
					Debug.Assert(fitness != null);
					Genome = genome;
					Fitness = fitness;
				}

				// ReSharper disable once MemberCanBePrivate.Local
				public readonly TGenome Genome;
				public readonly Fitness Fitness;

				public static implicit operator (TGenome Genome, Fitness Fitness) (GF gf)
					=> (gf?.Genome, gf?.Fitness);
			}

			GF _bestFitness;
			public (TGenome Genome, Fitness Fitness) BestFitness => _bestFitness;

			public bool UpdateBestFitness(TGenome genome, Fitness fitness)
			{
				if (genome == null) throw new ArgumentNullException(nameof(genome));
				if (fitness == null) throw new ArgumentNullException(nameof(fitness));
				Contract.EndContractBlock();

				var f = fitness.Clone() ?? throw new ArgumentNullException(nameof(fitness));

				GF contending = null;
				GF defending;
				while ((defending = _bestFitness) == null
					   || genome == defending.Genome && f.SampleCount > defending.Fitness.SampleCount
					   || f.Results.Average.IsGreaterThan(defending.Fitness.Results.Average))
				{
					contending = contending ?? new GF(genome, f);
					if (Interlocked.CompareExchange(ref _bestFitness, contending, defending) == defending)
						return true;
				}
				return false;
			}

		}

		// ReSharper disable once StaticMemberInGenericType
		static int ProblemCount;
		public int ID { get; } = Interlocked.Increment(ref ProblemCount);

		public IReadOnlyList<IProblemPool<TGenome>> Pools { get; }

		long _testCount;
		public long TestCount => _testCount;

		// ReSharper disable once MemberCanBeProtected.Global
		// ReSharper disable once NotAccessedField.Global
		public readonly ushort SampleSize;
		protected readonly int SampleSizeInt;

		public bool HasConverged { get; private set; }
		public void Converged() => HasConverged = true;

		protected ProblemBase(
			IEnumerable<(ReadOnlyMemory<Metric> Metrics, Func<TGenome, double[], Fitness> Transform)> fitnessTransators,
			ushort sampleSize,
			ushort championPoolSize)
		{
			SampleSize = sampleSize;
			SampleSizeInt = sampleSize;
			var c = championPoolSize;
			Pools = fitnessTransators?.Select(t => new Pool(c, t.Metrics, t.Transform)).ToList().AsReadOnly()
				?? throw new ArgumentNullException(nameof(fitnessTransators));
		}

		protected ProblemBase(
			ushort sampleSize,
			ushort championPoolSize,
			params (ReadOnlyMemory<Metric> Metrics, Func<TGenome, double[], Fitness> Transform)[] fitnessTranslators)
			: this(fitnessTranslators, sampleSize, championPoolSize) { }

		protected abstract double[] ProcessSampleMetrics(TGenome g, long sampleId);

		public IEnumerable<Fitness> ProcessSample(TGenome g, long sampleId)
		{
			var metrics = ProcessSampleMetrics(g, sampleId);
			Interlocked.Increment(ref _testCount);
			return Pools.Select(p => p.Transform(g, metrics));
		}

		// ReSharper disable once VirtualMemberNeverOverridden.Global
		protected virtual Task<double[]> ProcessSampleMetricsAsync(TGenome g, long sampleId)
			=> Task.Run(() => ProcessSampleMetrics(g, sampleId));

		public async ValueTask<IEnumerable<Fitness>> ProcessSampleAsync(TGenome g, long sampleId = 0)
		{
			var metrics = await ProcessSampleMetricsAsync(g, sampleId);
			Interlocked.Increment(ref _testCount);
			return Pools.Select(p => p.Transform(g, metrics));
		}

	}
}
