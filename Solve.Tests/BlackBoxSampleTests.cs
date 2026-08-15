using BlackBoxFunction;
using Open.Evaluation.Arithmetic;
using Open.Evaluation.Core;
using Solve.Evaluation;
using Solve.Metrics;

namespace Solve.Tests;

/// <summary>
/// Covers task 15-0011: SampleCache2 must draw independent, deterministic-per-id samples that
/// are not collinear and span negative/positive values, and BlackBoxFunction.Problem's Direction
/// metric must treat a degenerate (constant-sign or empty) target delta-sign sequence as neutral
/// (0) rather than the -2 NaN penalty, while still applying -2 when the undefined correlation is
/// caused by the genome's own output.
/// </summary>
public class BlackBoxSampleTests
{
	static double ConstFormula(IReadOnlyList<double> _) => 0.0;

	// Rank of an R x C matrix via Gaussian elimination with partial pivoting.
	private static int MatrixRank(double[,] matrix, double eps = 1e-9)
	{
		int rows = matrix.GetLength(0);
		int cols = matrix.GetLength(1);
		var a = (double[,])matrix.Clone();
		int rank = 0;

		for (var col = 0; col < cols && rank < rows; col++)
		{
			var pivot = -1;
			var best = eps;
			for (var r = rank; r < rows; r++)
			{
				var v = Math.Abs(a[r, col]);
				if (v > best) { best = v; pivot = r; }
			}

			if (pivot < 0) continue; // No usable pivot in this column.

			if (pivot != rank)
			{
				for (var c = 0; c < cols; c++)
					(a[rank, c], a[pivot, c]) = (a[pivot, c], a[rank, c]);
			}

			for (var r = rank + 1; r < rows; r++)
			{
				var factor = a[r, col] / a[rank, col];
				for (var c = col; c < cols; c++)
					a[r, c] -= factor * a[rank, c];
			}

			rank++;
		}

		return rank;
	}

	// AC1: samples for a given id are not collinear (matrix rank exceeds 1).
	//
	// Note: Entry.Values is deliberately constructed as an "endless" LazyList (see
	// SampleCache2.Entry), so Entry.Count throws until the list has been fully drained by
	// indexing. That's pre-existing behavior unrelated to this fix, so these tests use the
	// known sample size passed to the constructor rather than Entry.Count.
	[Fact]
	public void Get_SamplesAreNotCollinear()
	{
		const int sampleSize = 20;
		var cache = new SampleCache2(ConstFormula, sampleSize: sampleSize);
		var entry = cache.Get(42);

		const int dims = 3;
		var matrix = new double[sampleSize, dims];
		for (var i = 0; i < sampleSize; i++)
		{
			var input = entry[i].input;
			for (var d = 0; d < dims; d++)
				matrix[i, d] = input[d];
		}

		Assert.True(MatrixRank(matrix) > 1, "Sample matrix is collinear (rank <= 1).");
	}

	// AC2: input components span both negative and positive values.
	[Fact]
	public void Get_ComponentsSpanNegativeAndPositive()
	{
		const int sampleSize = 50;
		var cache = new SampleCache2(ConstFormula, sampleSize: sampleSize);
		var entry = cache.Get(7);

		double[] dim0 = [.. Enumerable.Range(0, sampleSize).Select(i => entry[i].input[0])];

		Assert.Contains(dim0, v => v < 0);
		Assert.Contains(dim0, v => v > 0);
	}

	// AC3: repeated calls with the same id return identical samples.
	[Fact]
	public void Get_RepeatedCallOnSameInstance_ReturnsSameCachedEntry()
	{
		var cache = new SampleCache2(ConstFormula, sampleSize: 10);
		var first = cache.Get(9);
		var second = cache.Get(9);

		Assert.Same(first, second);
	}

	[Fact]
	public void Get_SameId_IsDeterministicAcrossFreshInstances()
	{
		// Independent SampleCache2 instances (so results can't merely be coming from the same
		// ConcurrentDictionary cache) must still agree for the same id.
		const int sampleSize = 15;
		var cacheA = new SampleCache2(ConstFormula, sampleSize: sampleSize);
		var cacheB = new SampleCache2(ConstFormula, sampleSize: sampleSize);

		var a = cacheA.Get(123);
		var b = cacheB.Get(123);

		for (var i = 0; i < sampleSize; i++)
		{
			for (var d = 0; d < 3; d++)
				Assert.Equal(a[i].input[d], b[i].input[d]);
			Assert.Equal(a[i].correct, b[i].correct);
		}
	}

	// AC4: distinct ids return distinct sample sets.
	[Fact]
	public void Get_DistinctIds_ProduceDistinctSamples()
	{
		const int sampleSize = 15;
		var cache = new SampleCache2(ConstFormula, sampleSize: sampleSize);
		long[] ids = [1, 2, 3, 4];

		var flattened = ids
			.Select(id =>
			{
				var e = cache.Get(id);
				return (double[])[.. Enumerable.Range(0, sampleSize).SelectMany(i => new[] { e[i].input[0], e[i].input[1], e[i].input[2] })];
			})
			.ToArray();

		for (var i = 0; i < flattened.Length; i++)
		{
			for (var j = i + 1; j < flattened.Length; j++)
				Assert.NotEqual(flattened[i], flattened[j]);
		}
	}

	// AC5: a degenerate (constant) target yields a neutral Direction score, not the -2 penalty,
	// regardless of the genome's own (non-degenerate) behavior.
	[Fact]
	public void ProcessSample_DegenerateTargetDirection_IsNeutralZero()
	{
		var problem = BlackBoxFunction.Problem.Create(_ => 7.0, sampleSize: 20);
		var counter = new CounterRegistry();
		var factory = new NumericEvalGenomeFactory(counter);
		var genome = new EvalGenome<double>(factory.Catalog.GetParameter(0)); // Varies with input; target does not.

		var fitness = problem.ProcessSample(genome, sampleId: 1).First();
		double direction = fitness.MetricAverages.First(mv => mv.Metric.Name == "Direction").Value;

		Assert.Equal(0, direction);
	}

	[Fact]
	public void ProcessSample_DegenerateSingleSampleTargetDirection_IsNeutralZero()
	{
		// A single-sample level produces an empty delta sequence -- also degenerate.
		var problem = BlackBoxFunction.Problem.Create(p => p[0], sampleSize: 1);
		var counter = new CounterRegistry();
		var factory = new NumericEvalGenomeFactory(counter);
		var genome = new EvalGenome<double>(factory.Catalog.GetParameter(0));

		var fitness = problem.ProcessSample(genome, sampleId: 3).First();
		double direction = fitness.MetricAverages.First(mv => mv.Metric.Name == "Direction").Value;

		Assert.Equal(0, direction);
	}

	// AC5: the -2 penalty must remain when the correlation is undefined because of the genome's
	// own output (here, a constant genome) against a genuinely varying (non-degenerate) target.
	[Fact]
	public void ProcessSample_GenomeInducedNaNCorrelation_KeepsNegativeTwoPenalty()
	{
		var problem = BlackBoxFunction.Problem.Create(p => p[0], sampleSize: 50);
		var counter = new CounterRegistry();
		var factory = new NumericEvalGenomeFactory(counter);
		var genome = new EvalGenome<double>(factory.Catalog.GetConstant(5.0)); // Constant output.

		var fitness = problem.ProcessSample(genome, sampleId: 2).First();
		double direction = fitness.MetricAverages.First(mv => mv.Metric.Name == "Direction").Value;

		Assert.Equal(-2, direction);
	}
}
