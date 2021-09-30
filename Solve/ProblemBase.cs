using Open.Memory;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve
{
	public abstract class ProblemBase<TGenome> : IProblem<TGenome>
		where TGenome : IGenome
	{
		protected class Pool : IProblemPool<TGenome>
		{
			public Pool(ushort poolSize, in ImmutableArray<Metric> metrics, Func<TGenome, double[], Fitness> transform)
			{
				if (poolSize == 0) throw new ArgumentOutOfRangeException(nameof(poolSize), poolSize, "Must be at least 1.");
				Transform = transform ?? throw new ArgumentNullException(nameof(transform));
				Contract.EndContractBlock();

				Metrics = metrics;
				Champions = new RankedPool<TGenome>(poolSize);
			}

			public ImmutableArray<Metric> Metrics { get; }

			public Func<TGenome, double[], Fitness> Transform { get; }

			public RankedPool<TGenome> Champions { get; }

			class GF
			{
				public GF(TGenome genome, Fitness fitness)
				{
					Debug.Assert(genome is not null);
					Debug.Assert(fitness is not null);
					Genome = genome;
					Fitness = fitness;
				}

				// ReSharper disable once MemberCanBePrivate.Local
				public readonly TGenome Genome;
				public readonly Fitness Fitness;

				public static implicit operator (TGenome Genome, Fitness? Fitness)(GF? gf)
					=> (gf is null ? default! : gf.Genome, gf?.Fitness);
			}

			GF? _bestFitness;
			public (TGenome Genome, Fitness? Fitness) BestFitness => _bestFitness;

			public bool UpdateBestFitness(TGenome genome, Fitness fitness)
			{
				if (genome is null) throw new ArgumentNullException(nameof(genome));
				if (fitness is null) throw new ArgumentNullException(nameof(fitness));
				Contract.EndContractBlock();

				var f = fitness.Clone();

				GF? contending = null;
				GF? defending;
				while ((defending = _bestFitness) is null
					   || genome.Equals(defending.Genome) && f.SampleCount > defending.Fitness.SampleCount
					   || f.Results.Average.IsGreaterThan(defending.Fitness.Results.Average))
				{
					contending ??= new GF(genome, f);
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
			IEnumerable<(ImmutableArray<Metric> Metrics, Func<TGenome, double[], Fitness> Transform)> fitnessTransators,
			ushort sampleSize,
			ushort championPoolSize)
		{
			if (championPoolSize == 0) throw new ArgumentOutOfRangeException(nameof(championPoolSize), championPoolSize, "Must be at least 1.");

			SampleSize = sampleSize;
			SampleSizeInt = sampleSize;
			var c = championPoolSize;
			Pools = fitnessTransators?.Select(t => new Pool(c, t.Metrics, t.Transform)).ToList().AsReadOnly()
				?? throw new ArgumentNullException(nameof(fitnessTransators));
		}

		protected ProblemBase(
			ushort sampleSize,
			ushort championPoolSize,
			params (ImmutableArray<Metric> Metrics, Func<TGenome, double[], Fitness> Transform)[] fitnessTranslators)
			: this(fitnessTranslators, sampleSize, championPoolSize) { }

		protected abstract double[] ProcessSampleMetrics(TGenome g, long sampleId);

		public IEnumerable<Fitness> ProcessSample(TGenome g, long sampleId)
		{
			var metrics = ProcessSampleMetrics(g, sampleId);
			Interlocked.Increment(ref _testCount);
			return Pools.Select(p => p.Transform(g, metrics));
		}

		// ReSharper disable once VirtualMemberNeverOverridden.Global
		protected virtual async ValueTask<double[]> ProcessSampleMetricsAsync(TGenome g, long sampleId)
		{
			await Task.Yield();
			return ProcessSampleMetrics(g, sampleId);
		}

		public async ValueTask<IEnumerable<Fitness>> ProcessSampleAsync(TGenome g, long sampleId = 0)
		{
			var metrics = await ProcessSampleMetricsAsync(g, sampleId).ConfigureAwait(false);
			Interlocked.Increment(ref _testCount);
			return Pools.Select(p => p.Transform(g, metrics));
		}

	}
}
