using Open.Collections;
using Open.Evaluation;
using Open.Evaluation.Arithmetic;
using Open.Evaluation.Core;
using Solve.Metrics;

using EvaluationRegistry = Open.Evaluation.Registry;

namespace Solve.Evaluation;
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public partial class NumericEvalGenomeFactory : EvalGenomeFactoryBase<double>
{
	public NumericEvalGenomeFactory(CounterRegistry metrics)
		: base(metrics) { }

	public NumericEvalGenomeFactory(CounterRegistry metrics, IEnumerable<EvalGenome<double>> seeds)
		: base(metrics, seeds) { }

	public NumericEvalGenomeFactory(CounterRegistry metrics, params EvalGenome<double>[] seeds)
		: base(metrics, seeds) { }

	public NumericEvalGenomeFactory(CounterRegistry metrics, IEnumerable<string> seeds)
		: base(metrics) => InjectSeeds(seeds);

	public NumericEvalGenomeFactory(CounterRegistry metrics, params string[] seeds)
		: this(metrics, (IEnumerable<string>)seeds)
	{
	}

	protected void InjectSeeds(IEnumerable<string> seeds)
		=> InjectSeeds(seeds?.Select(s => Create(Catalog.Parse(s), ("Seed", null))));

	#region Constants fitting (25-0026)
	/// <summary>
	/// Opt-in switch for the constants-fitting pass (see <see cref="ConstantsFitting"/>). Off by
	/// default so existing callers/tests are unaffected; <c>BlackBox.Benchmark</c> threads a single
	/// command-line flag through to this property to run the A/B comparison for task 25-0026.
	/// </summary>
	public bool ConstantsFittingEnabled { get; set; }

	/// <summary>
	/// Wiring hook (AC5): when <see cref="ConstantsFittingEnabled"/> is set, attempts to fit
	/// <paramref name="genome"/>'s constant nodes against <paramref name="samples"/> and, if an
	/// improving fit was found, registers the fitted variant through this factory's normal
	/// <see cref="Registration(IEvaluate{double}, string, Action{EvalGenome{double}}?)"/> dedup path
	/// -- the same path used by structural generation, mutation, and crossover. Intended to be
	/// called from a champion-broadcast handler (e.g. <c>TowerScheme&lt;TGenome&gt;</c>'s
	/// <c>IObservable</c> champion subscription, as already consumed by <c>BlackBox.Benchmark</c>'s
	/// <c>scheme.Subscribe(...)</c>) so fitting only runs for genomes that already won a pool --
	/// applied selectively, not on every evaluation, keeping the added cost sublinear.
	/// </summary>
	/// <returns>
	/// The newly registered (or already-known, deduped) fitted genome, or <see langword="null"/>
	/// when fitting is disabled, the genome has no fittable constants, or no improving fit was found
	/// -- callers should treat a null result as "nothing new to do".
	/// </returns>
	public EvalGenome<double>? TryFitConstants(
		EvalGenome<double> genome,
		IReadOnlyList<(IReadOnlyList<double> Input, double Target)> samples,
		int maxIterations = ConstantsFitting.DefaultMaxIterations,
		TimeSpan? timeBudget = null)
	{
		if (!ConstantsFittingEnabled) return null;
		ArgumentNullException.ThrowIfNull(genome);

		EvalGenome<double> fitted = ConstantsFitting.Fit(Catalog, genome, samples, maxIterations, timeBudget);
		if (ReferenceEquals(fitted, genome)) return null; // No improving fit; nothing new to register.

		return Registration(fitted.Root, ("ConstantsFitting > Champion", genome.Hash));
	}
	#endregion

	#region Operated
	protected override IEnumerable<EvalGenome<double>> GenerateOperated(ushort paramCount = 2)
	{
		if (paramCount < 2)
		{
			throw new ArgumentOutOfRangeException(nameof(paramCount), paramCount,
				"Must have at least 2 parameter count.");
		}

		System.Collections.Immutable.ImmutableArray<char> operators = EvaluationRegistry.Arithmetic.Operators;

		return UShortRange(0, paramCount)
			.Combinations(paramCount)
			.SelectMany(combination =>
			{
				Parameter[] children = combination.Select(p => Catalog.GetParameter(p)).ToArray();
				return operators.Select(op =>
					Registration(
						EvaluationRegistry.Arithmetic.GetOperator(Catalog, op, children),
						$"EvalGenomeFactory.GenerateOperated({paramCount})"));
			});
	}
	#endregion

	#region Functions
	protected override IEnumerable<EvalGenome<double>> GenerateFunctioned(ushort id)
	{
		Parameter p = Catalog.GetParameter(id);
		foreach (char op in EvaluationRegistry.Arithmetic.Functions)
		{
			switch (op)
			{
				case EvaluationRegistry.Arithmetic.SQUARE:
					yield return Registration(Catalog.GetExponent(p, 2), "GenerateFunctioned > Square of Parameter");
					break;
				case EvaluationRegistry.Arithmetic.INVERT:
					yield return Registration(Catalog.GetExponent(p, -1), "GenerateFunctioned > Division by Parameter");
					break;
				case EvaluationRegistry.Arithmetic.SQUARE_ROOT:
					yield return Registration(Catalog.GetExponent(p, 0.5), "GenerateFunctioned > Square Root of Parameter");
					break;
			}
		}
	}
	#endregion

}
