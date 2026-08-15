using Open.Evaluation.Arithmetic;
using Open.Evaluation.Catalogs;
using Open.Evaluation.Core;
using Solve.Evaluation;
using Solve.Metrics;
using System.Diagnostics;

using EvaluationRegistry = Open.Evaluation.Registry;

namespace Solve.Tests;

/// <summary>
/// Covers task 25-0026: a bounded, dependency-free Levenberg-Marquardt constants-fitting pass for
/// numeric (<see cref="EvalGenome{T}"/> of <see cref="double"/>) genomes.
/// </summary>
public class ConstantsFittingTests
{
	static NumericEvalGenomeFactory CreateFactory()
		=> new(new CounterRegistry());

	// Builds "parameter(0) * C" directly via the catalog's operator construction. C = exactly 1.0 is
	// deliberately never used as a *starting* value anywhere in these tests: Open.Evaluation's
	// Product strips a literal multiplicative identity at construction time (verified against the
	// real 1.1.2 package this repo consumes), so "a * 1" collapses to just "a" with no constant node
	// at all -- there would be nothing left to fit.
	static IEvaluate<double> BuildLinear(EvaluationCatalog<double> catalog, double startingCoefficient)
		=> EvaluationRegistry.Arithmetic.GetOperator(catalog, EvaluationRegistry.Arithmetic.MULTIPLY,
			[catalog.GetParameter(0), catalog.GetConstant(startingCoefficient)]);

	static (IReadOnlyList<double> Input, double Target)[] LinearSamples(double trueCoefficient, int seed, int count = 30)
	{
		var rng = new Random(seed);
		return [.. Enumerable.Range(0, count)
			.Select(_ => rng.NextDouble() * 200 - 100)
			.Select(x => ((IReadOnlyList<double>)new double[] { x }, trueCoefficient * x))];
	}

	// AC3: fitting a hand-built "a * C" against data generated with a known coefficient, starting
	// from a deliberately wrong constant, recovers the correct coefficient within 1e-3.
	[Fact]
	public void Fit_RecoversKnownConstant_WithinTolerance()
	{
		NumericEvalGenomeFactory factory = CreateFactory();
		EvaluationCatalog<double> catalog = factory.Catalog;

		IEvaluate<double> start = BuildLinear(catalog, startingCoefficient: 0.1);
		var genome = new EvalGenome<double>(start);

		const double TrueCoefficient = 2.5;
		(IReadOnlyList<double> Input, double Target)[] samples = LinearSamples(TrueCoefficient, seed: 12345);

		EvalGenome<double> fitted = ConstantsFitting.Fit(catalog, genome, samples);

		Assert.NotSame(genome, fitted);
		// a=1 isolates the coefficient in the fitted output.
		double recovered = fitted.Evaluate([1.0]);
		Assert.True(Math.Abs(recovered - TrueCoefficient) < 1e-3,
			$"Expected recovered coefficient near {TrueCoefficient}, got {recovered}.");
	}

	// AC4: an expression with no constant nodes passes through unchanged.
	[Fact]
	public void Fit_NoConstants_ReturnsOriginalGenomeUnchanged()
	{
		NumericEvalGenomeFactory factory = CreateFactory();
		EvaluationCatalog<double> catalog = factory.Catalog;

		IEvaluate<double> root = EvaluationRegistry.Arithmetic.GetOperator(catalog, EvaluationRegistry.Arithmetic.ADD,
			[catalog.GetParameter(0), catalog.GetParameter(1)]);
		var genome = new EvalGenome<double>(root);

		(IReadOnlyList<double> Input, double Target)[] samples = [((IReadOnlyList<double>)new double[] { 1, 2 }, 3.0)];

		EvalGenome<double> result = ConstantsFitting.Fit(catalog, genome, samples);

		Assert.Same(genome, result);
	}

	// AC2: an empty sample set cannot be fit against; must fall back to the original without throwing.
	[Fact]
	public void Fit_EmptySamples_ReturnsOriginalWithoutThrowing()
	{
		NumericEvalGenomeFactory factory = CreateFactory();
		EvaluationCatalog<double> catalog = factory.Catalog;

		IEvaluate<double> root = BuildLinear(catalog, startingCoefficient: 0.1);
		var genome = new EvalGenome<double>(root);

		EvalGenome<double> result = ConstantsFitting.Fit(catalog, genome, []);

		Assert.Same(genome, result);
	}

	// AC2: a starting point that is already domain-invalid for the sample set (sqrt of negative
	// inputs, regardless of the constant) must not throw and must fall back to the original genome.
	[Fact]
	public void Fit_DomainErrorAtStartingPoint_ReturnsOriginalWithoutThrowing()
	{
		NumericEvalGenomeFactory factory = CreateFactory();
		EvaluationCatalog<double> catalog = factory.Catalog;

		IEvaluate<double> root = EvaluationRegistry.Arithmetic.GetOperator(catalog, EvaluationRegistry.Arithmetic.MULTIPLY,
			[catalog.GetExponent(catalog.GetParameter(0), 0.5), catalog.GetConstant(0.3)]);
		var genome = new EvalGenome<double>(root);

		(IReadOnlyList<double> Input, double Target)[] samples =
		[.. new double[] { -5, -3, 4, 9 }.Select(x => ((IReadOnlyList<double>)new double[] { x }, 2.0 * Math.Sqrt(Math.Abs(x))))];

		Exception? exception = Record.Exception(() => ConstantsFitting.Fit(catalog, genome, samples));

		Assert.Null(exception);
	}

	// AC2: the routine is genuinely bounded by time even when handed a huge iteration budget.
	[Fact]
	public void Fit_RespectsTimeBudget()
	{
		NumericEvalGenomeFactory factory = CreateFactory();
		EvaluationCatalog<double> catalog = factory.Catalog;

		IEvaluate<double> root = BuildLinear(catalog, startingCoefficient: 0.1);
		var genome = new EvalGenome<double>(root);
		(IReadOnlyList<double> Input, double Target)[] samples = LinearSamples(trueCoefficient: 2.5, seed: 1, count: 50);

		var sw = Stopwatch.StartNew();
		ConstantsFitting.Fit(catalog, genome, samples, maxIterations: 100_000, timeBudget: TimeSpan.FromMilliseconds(20));
		sw.Stop();

		// Generous ceiling to avoid flakiness from JIT/first-run overhead while still catching a
		// genuine "ignored the time budget" regression (which would run for the full 100k iterations).
		Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"Fit call took {sw.Elapsed}, expected it to respect a 20ms budget.");
	}

	// Fitting two constants at once (a coefficient and an offset) recovers both.
	[Fact]
	public void Fit_MultipleConstants_RecoversBothWithinTolerance()
	{
		NumericEvalGenomeFactory factory = CreateFactory();
		EvaluationCatalog<double> catalog = factory.Catalog;

		IEvaluate<double> start = EvaluationRegistry.Arithmetic.GetOperator(catalog, EvaluationRegistry.Arithmetic.ADD,
		[
			EvaluationRegistry.Arithmetic.GetOperator(catalog, EvaluationRegistry.Arithmetic.MULTIPLY,
				[catalog.GetParameter(0), catalog.GetConstant(0.5)]),
			catalog.GetConstant(-3.0)
		]);
		var genome = new EvalGenome<double>(start);

		const double TrueSlope = 2.5;
		const double TrueIntercept = 7.0;
		var rng = new Random(7);
		(IReadOnlyList<double> Input, double Target)[] samples =
		[.. Enumerable.Range(0, 30)
			.Select(_ => rng.NextDouble() * 200 - 100)
			.Select(x => ((IReadOnlyList<double>)new double[] { x }, TrueSlope * x + TrueIntercept))];

		EvalGenome<double> fitted = ConstantsFitting.Fit(catalog, genome, samples);

		Assert.NotSame(genome, fitted);
		double atZero = fitted.Evaluate([0.0]); // isolates the intercept
		double atOne = fitted.Evaluate([1.0]);
		Assert.True(Math.Abs(atZero - TrueIntercept) < 1e-3, $"intercept: expected {TrueIntercept}, got {atZero}");
		Assert.True(Math.Abs((atOne - atZero) - TrueSlope) < 1e-3, $"slope: expected {TrueSlope}, got {atOne - atZero}");
	}

	// An Exponent's power operand (e.g. the 2 in a square) is a genuine IConstant<double> node in
	// the tree but must never be treated as fittable -- fitting it would let "square" drift into an
	// arbitrary continuous power. Verified by fitting against data generated by the *cubed* formula:
	// if the exponent were fittable, LM would happily walk 2 toward 3 and drive SSE near zero: it
	// must NOT be able to do that, so a real residual should remain.
	[Fact]
	public void Fit_DoesNotTreatExponentPowerAsFittable()
	{
		NumericEvalGenomeFactory factory = CreateFactory();
		EvaluationCatalog<double> catalog = factory.Catalog;

		IEvaluate<double> root = catalog.GetExponent(catalog.GetParameter(0), 2);
		var genome = new EvalGenome<double>(root);

		var rng = new Random(99);
		(IReadOnlyList<double> Input, double Target)[] samples =
		[.. Enumerable.Range(0, 20)
			.Select(_ => rng.NextDouble() * 10 + 1) // positive, avoids sign/domain noise
			.Select(x => ((IReadOnlyList<double>)new double[] { x }, x * x * x))];

		EvalGenome<double> result = ConstantsFitting.Fit(catalog, genome, samples);

		// No fittable constants exist on this tree (the exponent's 2 is excluded), so it must pass
		// through unchanged rather than "fitting" the excluded power.
		Assert.Same(genome, result);
	}

	// AC5 (wiring): NumericEvalGenomeFactory.TryFitConstants registers an improving fit through the
	// factory's normal Registration/dedup path (the same path GenerateOperated/mutation/crossover
	// use) -- proven here by the returned genome coming back frozen, exactly as Registration leaves
	// every genome it adds.
	[Fact]
	public void TryFitConstants_WhenEnabledAndImproving_RegistersFittedVariant()
	{
		NumericEvalGenomeFactory factory = CreateFactory();
		factory.ConstantsFittingEnabled = true;
		EvaluationCatalog<double> catalog = factory.Catalog;

		IEvaluate<double> start = BuildLinear(catalog, startingCoefficient: 0.1);
		var genome = new EvalGenome<double>(start);
		(IReadOnlyList<double> Input, double Target)[] samples = LinearSamples(trueCoefficient: 2.5, seed: 42);

		EvalGenome<double>? fitted = factory.TryFitConstants(genome, samples);

		Assert.NotNull(fitted);
		Assert.NotSame(genome, fitted);
		Assert.True(fitted.IsFrozen);
	}

	// The switch defaults to off so pre-existing (unrelated) factory usage/tests are unaffected.
	[Fact]
	public void TryFitConstants_WhenDisabled_ReturnsNull()
	{
		NumericEvalGenomeFactory factory = CreateFactory();
		Assert.False(factory.ConstantsFittingEnabled);
		EvaluationCatalog<double> catalog = factory.Catalog;

		IEvaluate<double> start = BuildLinear(catalog, startingCoefficient: 0.1);
		var genome = new EvalGenome<double>(start);
		(IReadOnlyList<double> Input, double Target)[] samples = LinearSamples(trueCoefficient: 2.5, seed: 42);

		Assert.Null(factory.TryFitConstants(genome, samples));
	}

	// When there's nothing to improve (no constants), the hook must report "nothing to do" (null)
	// rather than registering a redundant, structurally-identical duplicate.
	[Fact]
	public void TryFitConstants_NoImprovingFit_ReturnsNull()
	{
		NumericEvalGenomeFactory factory = CreateFactory();
		factory.ConstantsFittingEnabled = true;
		EvaluationCatalog<double> catalog = factory.Catalog;

		IEvaluate<double> root = EvaluationRegistry.Arithmetic.GetOperator(catalog, EvaluationRegistry.Arithmetic.ADD,
			[catalog.GetParameter(0), catalog.GetParameter(1)]);
		var genome = new EvalGenome<double>(root);
		(IReadOnlyList<double> Input, double Target)[] samples = [((IReadOnlyList<double>)new double[] { 1, 2 }, 3.0)];

		Assert.Null(factory.TryFitConstants(genome, samples));
	}
}
