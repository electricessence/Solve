using Open.Memory;
using Solve.Telemetry;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace Solve;

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

		private sealed class GF
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

		private GF? _bestFitness;
		public (TGenome Genome, Fitness? Fitness) BestFitness => _bestFitness;

		public bool UpdateBestFitness(TGenome genome, Fitness fitness)
		{
			if (genome is null) throw new ArgumentNullException(nameof(genome));
			ArgumentNullException.ThrowIfNull(fitness);
			Contract.EndContractBlock();

			Fitness f = fitness.Clone();

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
	private static int ProblemCount;
	public int ID { get; } = Interlocked.Increment(ref ProblemCount);

	public IReadOnlyList<IProblemPool<TGenome>> Pools { get; }

	private long _testCount;
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
		ushort c = championPoolSize;
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
		// 15-0030: solve.evaluations / solve.evaluation.duration, timed around the actual
		// evaluation call -- the same call this method's Interlocked.Increment(ref _testCount)
		// below already counts -- via Stopwatch.GetTimestamp()/GetElapsedTime rather than a
		// heap-allocated Stopwatch instance.
		long start = Stopwatch.GetTimestamp();
		double[] metrics = ProcessSampleMetrics(g, sampleId);
		EngineInstruments.RecordEvaluation(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		Interlocked.Increment(ref _testCount);
		return Pools.Select(p => p.Transform(g, metrics));
	}

	// ReSharper disable once VirtualMemberNeverOverridden.Global
	// The Task.Yield() this used to open with only forced a thread-pool hop; it didn't add
	// concurrency, since whatever chain called ProcessSampleAsync was still the only thing
	// running that evaluation. Concurrency now comes from TowerScheme's shared evaluation
	// worker stage (see TowerScheme.ProblemTower's evaluation queue), which already runs
	// each call to this method on its own worker — so the yield is redundant overhead here.
	protected virtual ValueTask<double[]> ProcessSampleMetricsAsync(TGenome g, long sampleId)
		=> new(ProcessSampleMetrics(g, sampleId));

	public async ValueTask<IEnumerable<Fitness>> ProcessSampleAsync(TGenome g, long sampleId = 0)
	{
		// 15-0030: see ProcessSample's identical instrumentation above for rationale.
		long start = Stopwatch.GetTimestamp();
		double[] metrics = await ProcessSampleMetricsAsync(g, sampleId).ConfigureAwait(false);
		EngineInstruments.RecordEvaluation(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
		Interlocked.Increment(ref _testCount);
		return Pools.Select(p => p.Transform(g, metrics));
	}

	/// <summary>
	/// Framework-level default for task 25-0023's per-case surface
	/// (<see cref="IProblem{TGenome}.ProcessSampleCasesAsync"/>): returns
	/// <see langword="null"/>, same as the interface member's own default-interface-method
	/// fallback -- a subclass that hasn't opted into per-case exposure (via <see langword="override"/>
	/// here) stays opted out of epsilon-lexicase ranking.
	/// </summary>
	/// <remarks>
	/// Declared here as a concrete <see langword="virtual"/> method -- rather than relying
	/// solely on <see cref="IProblem{TGenome}.ProcessSampleCasesAsync"/>'s own default interface
	/// method -- specifically so a subclass (e.g. <c>Eater.Problem</c>) can participate in
	/// <see cref="IProblem{TGenome}"/>'s dispatch by declaring an ordinary
	/// <see langword="override"/>. Interface member binding is fixed at the type that first
	/// lists the interface (this class); a same-signature method added on a more-derived type
	/// with no corresponding <see langword="virtual"/> member here would NOT bind into
	/// <see cref="IProblem{TGenome}"/>'s dispatch slot at all -- calls made through an
	/// <see cref="IProblem{TGenome}"/>-typed reference (as <c>TowerScheme{TGenome}.Level</c>
	/// always uses -- see <c>ProblemTower.Problem</c>'s declared type) would keep hitting the
	/// interface's own default (this same "return null" behavior) regardless, silently
	/// stranding the subclass's intended override. A virtual member here closes that gap.
	/// </remarks>
	public virtual ValueTask<IReadOnlyList<CaseResult>?> ProcessSampleCasesAsync(TGenome g, long sampleId)
		=> new((IReadOnlyList<CaseResult>?)null);
}
