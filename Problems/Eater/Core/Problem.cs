using Solve;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Eater;

public class Problem : ProblemBase<Genome>
{
	public readonly SampleCache Samples;

	protected Problem(
		ushort gridSize = 10,
		ushort sampleSize = 40,
		ushort championPoolSize = 100,
		params (ImmutableArray<Metric> Metrics, Func<Genome, double[], Fitness> Transform)[] fitnessTranslators)
		: base(fitnessTranslators, sampleSize, championPoolSize) => Samples = new SampleCache(gridSize);

	const int FoodFoundRate = 0;
	const int AverageEnergy = 1;
	const int AverageWasted = 2;
	const int GeneCount = 3;

	// Synthetic key: not present in the raw per-sample metrics array (values); it is derived
	// from FoodFoundRate via BandFoodFoundRate(...) inside GetBandedMetricValues.
	const int BandedFoodFoundRate = 4;

	// Element count of the raw per-sample metrics array produced by ProcessSampleMetrics
	// (FoodFoundRate, AverageEnergy, AverageWasted) -- distinct from the metric-ID constants
	// above (GeneCount == 3 is coincidental, not a sizing relationship).
	const int RawMetricCount = AverageWasted + 1;

	// Reused across calls on the same thread to avoid a fresh double[] allocation per
	// evaluation at sustained high evaluation rates. Safe because every caller (ProblemBase's
	// ProcessSample/ProcessSampleAsync, both effectively synchronous for this problem since
	// ProcessSampleMetricsAsync is not overridden here) fully consumes the returned array's
	// values before this method can be re-entered on the same thread -- see ProblemBase.cs's
	// eager `Pools.Select(...)` consumption immediately following the call. TestAllSamples
	// (the only path that hands this array to a caller who might hold onto it) clones it
	// before returning to preserve that method's fresh-array contract.
	[ThreadStatic]
	private static double[]? _sampleMetricsBuffer;

	static Fitness GetPrimaryMetricValues(ImmutableArray<Metric> metrics, Genome genome, double[] values)
	{
		int len = metrics.Length;
		var result = ImmutableArray.CreateBuilder<double>(metrics.Length);
		result.Count = len;
		for (int i = 0; i < len; i++)
		{
			var metric = metrics[i];
			result[i] = metric.ID switch
			{
				FoodFoundRate => values[FoodFoundRate],
				AverageEnergy => -values[AverageEnergy],
				AverageWasted => -values[AverageWasted],
				GeneCount => -genome.GeneCount,
				_ => throw new IndexOutOfRangeException()
			};
		}

		return new Fitness(metrics, result.MoveToImmutable());
	}

	protected static readonly ImmutableArray<Metric> MetricsPrimary
		= [
			// Tolerance 0: Food-Found-Rate is an exact ratio of integer counts (found / count),
			// so a genome that finds every food item on every sample lands on precisely 1.0 in
			// double precision — no floating-point slack is needed, and zero tolerance means
			// only genuinely perfect, sustained coverage is reported as converged.
			new Metric(FoodFoundRate, "Food-Found-Rate", "Food-Found-Rate {0:p}", 1, 0),
			new Metric(AverageEnergy, "Average-Energy", "Average-Energy {0:n3}"),
			new Metric(AverageWasted, "Average-Wasted", "Average-Wasted {0:n3}"),
			new Metric(GeneCount, "Gene-Count", "Gene-Count {0:n0}"),
		];

	protected static Fitness FitnessPrimary(Genome genome, double[] metrics)
		=> GetPrimaryMetricValues(MetricsPrimary, genome, metrics);

	protected static readonly ImmutableArray<Metric> MetricsSecondary01
		= [MetricsPrimary[FoodFoundRate], MetricsPrimary[AverageEnergy], MetricsPrimary[GeneCount]];

	protected static Fitness FitnessSecondary01(Genome genome, double[] metrics)
		=> GetSecondaryMetricValues(MetricsSecondary01, genome, metrics);

	protected static readonly ImmutableArray<Metric> MetricsSecondary02
		= [MetricsPrimary[FoodFoundRate], MetricsPrimary[GeneCount], MetricsPrimary[AverageEnergy]];

	protected static Fitness FitnessSecondary02(Genome genome, double[] metrics)
		=> GetSecondaryMetricValues(MetricsSecondary02, genome, metrics);

	static Fitness GetSecondaryMetricValues(ImmutableArray<Metric> metrics, Genome genome, double[] values)
	{
		int len = metrics.Length;
		var result = ImmutableArray.CreateBuilder<double>(metrics.Length);
		result.Count = len;
		for (int i = 0; i < len; i++)
		{
			var metric = metrics[i];
			result[i] = metric.ID switch
			{
				FoodFoundRate => values[FoodFoundRate],
				AverageEnergy => -values[AverageEnergy] - values[AverageWasted] * 2,
				GeneCount => -genome.GeneCount,
				_ => throw new IndexOutOfRangeException()
			};
		}

		return new Fitness(metrics, result.MoveToImmutable());
	}

	/// <summary>
	/// Quantizes a raw Food-Found-Rate into bands of width <paramref name="band"/> (default
	/// 0.05) via <c>Math.Floor(rate / band) * band</c>, so genomes with near-perfect but
	/// non-identical coverage (e.g. 0.97 and 1.0) land in the same band and can be ranked
	/// instead by a cheaper secondary key -- typically gene count -- letting selection
	/// ridge-step toward smaller genomes instead of being eliminated outright by lenses that
	/// rank exact Food-Found-Rate first.
	/// </summary>
	/// <remarks>
	/// Two floating-point-precision guards are required around the literal formula:
	/// <list type="bullet">
	/// <item>A tiny epsilon is added before flooring, because a raw rate that is an exact
	/// multiple of <paramref name="band"/> (e.g. 0.95) can divide down to a value fractionally
	/// below the intended integer (e.g. 18.999999999999996), which would floor to 18 instead of
	/// 19 and misband it a full band low.</item>
	/// <item>The result is capped at the top band's lower bound so that a perfect 1.0 rate --
	/// which is its own exact quotient (rate / band == the band count) -- ties with the rest of
	/// the top band (e.g. 0.97) instead of forming its own isolated band above it.</item>
	/// </list>
	/// </remarks>
	public static double BandFoodFoundRate(double rate, double band = 0.05)
	{
		if (band <= 0 || band > 1)
			throw new ArgumentOutOfRangeException(nameof(band), band, "Must be greater than 0 and no greater than 1.");

		const double epsilon = 1e-9;
		double banded = Math.Floor(rate / band + epsilon) * band;
		double topBand = Math.Floor(1.0 / band - epsilon) * band;
		return Math.Min(banded, topBand);
	}

	protected static readonly ImmutableArray<Metric> MetricsSecondaryBanded
		= [
			new Metric(BandedFoodFoundRate, "Food-Found-Rate (Banded)", "Food-Found-Rate (Banded) {0:p}", 1),
			MetricsPrimary[GeneCount],
			MetricsPrimary[AverageEnergy],
		];

	protected static Fitness FitnessSecondaryBanded(Genome genome, double[] metrics, double band = 0.05)
		=> GetBandedMetricValues(MetricsSecondaryBanded, band, genome, metrics);

	static Fitness GetBandedMetricValues(ImmutableArray<Metric> metrics, double band, Genome genome, double[] values)
	{
		int len = metrics.Length;
		var result = ImmutableArray.CreateBuilder<double>(metrics.Length);
		result.Count = len;
		for (int i = 0; i < len; i++)
		{
			var metric = metrics[i];
			result[i] = metric.ID switch
			{
				BandedFoodFoundRate => BandFoodFoundRate(values[FoodFoundRate], band),
				AverageEnergy => -values[AverageEnergy] - values[AverageWasted] * 2,
				GeneCount => -genome.GeneCount,
				_ => throw new IndexOutOfRangeException()
			};
		}

		return new Fitness(metrics, result.MoveToImmutable());
	}

	protected static Fitness Fitness02(Genome genome, double[] metrics)
		=> GetPrimaryMetricValues(Metrics02, genome, metrics);

	protected static Fitness Fitness03(Genome genome, double[] metrics)
		=> GetPrimaryMetricValues(Metrics03, genome, metrics);

	protected static readonly ImmutableArray<Metric> Metrics02 = [MetricsPrimary[FoodFoundRate], MetricsPrimary[AverageWasted], MetricsPrimary[GeneCount], MetricsPrimary[AverageEnergy]];

	protected static readonly ImmutableArray<Metric> Metrics03 = [MetricsPrimary[FoodFoundRate], MetricsPrimary[GeneCount], MetricsPrimary[AverageEnergy], MetricsPrimary[AverageWasted]];

	protected override double[] ProcessSampleMetrics(Genome g, long sampleId)
	{
		var boundary = Samples.Boundary;
		var samples = Samples.Get((int)sampleId);
		samples = sampleId == -1 ? samples : samples.Take(SampleSize);
		double found = 0;
		double energy = 0;
		double wasted = 0;

		int count = 0;
		foreach (var s in samples)
		{
			count++;
			bool success = g.Try(boundary, s.EaterStart, s.Food, out int e, out int w);
			if (success) found++;

			// Re-simulating the reduced genome for every sample is expensive (a full
			// reduce + re-parse + re-simulate per sample), even though the check itself is
			// logically correct. It compiles in only when GENOME_DIAGNOSTICS is explicitly
			// opted into, not on plain DEBUG.
#if DEBUG && GENOME_DIAGNOSTICS
			Debug.Assert(!g.TryReduce(out var red) || success == red.Try(boundary, s.EaterStart, s.Food),
				"Reduced version should match.");
#endif

			energy += e;
			wasted += w;
		}

		Debug.Assert(g.Hash.Length != 0 || found == 0,
			"An empty has should yield no results.");

		double averageEnergy = energy / count;
		double averageWasted = wasted / count;

		double[] result = _sampleMetricsBuffer ??= new double[RawMetricCount];
		result[FoodFoundRate] = found / count;
		result[AverageEnergy] = averageEnergy;
		result[AverageWasted] = averageWasted;
		return result;
	}

	public double[] TestAllSamples(Genome g)
		=> (double[])ProcessSampleMetrics(g, -1).Clone();

	/// <summary>
	/// Eater's implementation of task 25-0023's per-case surface (see
	/// <see cref="Solve.IProblem{TGenome}.ProcessSampleCasesAsync"/>): one
	/// <see cref="CaseResult"/> per grid sample in the same sample set
	/// <see cref="ProcessSampleMetrics"/> would aggregate for <paramref name="sampleId"/> --
	/// same <see cref="SampleCache"/> lookup, same <c>Take(SampleSize)</c> bound, so case index
	/// <c>k</c> here corresponds to the same underlying trial that contributed to index
	/// <c>k</c>'s share of that call's aggregate sums (both draw from the same memoized,
	/// sampleId-keyed sequence -- see <see cref="SampleCache.Get"/>).
	/// </summary>
	/// <remarks>
	/// <see cref="CaseResult.Success"/> is the trial's own "found its food" outcome.
	/// <see cref="CaseResult.Value"/> is the negated energy this trial spent -- the same sign
	/// flip <see cref="GetPrimaryMetricValues"/> already applies to
	/// <c>values[AverageEnergy]</c>, so higher is better here exactly as it is everywhere else
	/// this framework reports a fitness-oriented value. This re-simulates the genome
	/// independently of <see cref="ProcessSampleMetrics"/> (a second, separate pass over the
	/// same samples) rather than sharing state with it: the two run on whatever thread the
	/// tower's shared evaluation workers happen to schedule them on, and
	/// <see cref="ProcessSampleMetrics"/>'s <see cref="_sampleMetricsBuffer"/> reuse is only
	/// safe under its own single-buffer-per-call contract, not a shared one across two
	/// independently-invoked methods. Only called at all when
	/// <see cref="Solve.ProcessingSchemes.ISchemeConfig.RankingMode"/> is
	/// <see cref="Solve.ProcessingSchemes.RankingMode.EpsilonLexicase"/> (see
	/// <c>TowerScheme{TGenome}.Level.ProcessContenderSafelyAsync</c>), so this extra simulation
	/// pass costs nothing on the default Aggregate path.
	/// </remarks>
	public override ValueTask<IReadOnlyList<CaseResult>?> ProcessSampleCasesAsync(Genome g, long sampleId)
	{
		var boundary = Samples.Boundary;
		IEnumerable<SampleCache.Entry> samples = Samples.Get((int)sampleId);
		samples = sampleId == -1 ? samples : samples.Take(SampleSize);

		var results = new List<CaseResult>(SampleSizeInt);
		foreach (SampleCache.Entry s in samples)
		{
			bool success = g.Try(boundary, s.EaterStart, s.Food, out int energy, out _);
			results.Add(new CaseResult(success, -energy));
		}

		return new ValueTask<IReadOnlyList<CaseResult>?>(results);
	}

	public static Problem CreateFitnessPrimary(
		ushort gridSize = 10,
		ushort sampleSize = 40,
		ushort championPoolSize = 100)
		=> new(gridSize, sampleSize, championPoolSize, (MetricsPrimary, FitnessPrimary));

	public static Problem CreateFitnessSecondary(
		ushort gridSize = 10,
		ushort sampleSize = 40,
		ushort championPoolSize = 100)
		=> new(gridSize, sampleSize, championPoolSize, (MetricsSecondary01, FitnessSecondary01), (MetricsSecondary02, FitnessSecondary02));

	/// <summary>
	/// Same pools as <see cref="CreateFitnessSecondary"/> plus a third lens whose first ranking
	/// key is a banded (quantized) Food-Found-Rate rather than the exact value -- see
	/// <see cref="BandFoodFoundRate"/> -- so genomes with near-perfect coverage but far fewer
	/// genes can survive selection alongside the strict pools' pristine elites.
	/// </summary>
	/// <param name="band">Band granularity applied to Food-Found-Rate in the third pool. Default 0.05.</param>
	public static Problem CreateFitnessSecondaryWithBandedLens(
		ushort gridSize = 10,
		ushort sampleSize = 40,
		ushort championPoolSize = 100,
		double band = 0.05)
		=> new(gridSize, sampleSize, championPoolSize,
			(MetricsSecondary01, FitnessSecondary01),
			(MetricsSecondary02, FitnessSecondary02),
			(MetricsSecondaryBanded, (genome, metrics) => FitnessSecondaryBanded(genome, metrics, band)));

	public static Problem CreateF02(
		ushort gridSize = 10,
		ushort sampleSize = 40,
		ushort championPoolSize = 100)
		=> new(gridSize, sampleSize, championPoolSize, (Metrics02, Fitness02));

	public static Problem CreateF0102(
		ushort gridSize = 10,
		ushort sampleSize = 40,
		ushort championPoolSize = 100)
		=> new(gridSize, sampleSize, championPoolSize, (MetricsPrimary, FitnessPrimary), (Metrics02, Fitness02));

	public static Problem CreateF010203(
		ushort gridSize = 10,
		ushort sampleSize = 40,
		ushort championPoolSize = 100)
		=> new(gridSize, sampleSize, championPoolSize, (MetricsPrimary, FitnessPrimary), (Metrics02, Fitness02), (Metrics03, Fitness03));
}
