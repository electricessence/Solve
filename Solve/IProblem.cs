/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using System.Collections.Immutable;

namespace Solve;

public interface IProblemPool<TGenome>
	 where TGenome : IGenome
{
	ImmutableArray<Metric> Metrics { get; }
	Func<TGenome, double[], Fitness> Transform { get; }

	(TGenome Genome, Fitness? Fitness) BestFitness { get; }
	bool UpdateBestFitness(TGenome genome, Fitness fitness);

	RankedPool<TGenome> Champions { get; }
}

/// <summary>
/// One test case's raw contribution to a genome's evaluation -- e.g. a single grid trial for
/// Eater, or a single input/expected-output pair for a black-box function -- surfaced by
/// <see cref="IProblem{TGenome}.ProcessSampleCasesAsync"/> for task 25-0023's epsilon-lexicase
/// ranking mode (<see cref="Solve.ProcessingSchemes.RankingMode.EpsilonLexicase"/>).
/// </summary>
/// <param name="Success">
/// The case's primary, categorical outcome (e.g. Eater's "did this trial find its food").
/// Lexicase treats this as a hard boundary rather than an epsilon-tolerant one: on any case
/// where at least one cohort survivor succeeded, every survivor that did not succeed on that
/// case is eliminated outright -- see
/// <c>TowerScheme{TGenome}.RankByEpsilonLexicase</c> for the exact rule.
/// </param>
/// <param name="Value">
/// A continuous secondary measure for this case, oriented the same way every other fitness
/// value in this framework already is -- <b>higher is better</b> (e.g. Eater passes the
/// negative of energy consumed, mirroring the negation its own aggregate
/// <c>Problem.GetPrimaryMetricValues</c> already applies to the same raw quantity). Used only to
/// rank/epsilon-filter entries that agree on <paramref name="Success"/> for this case.
/// </param>
public readonly record struct CaseResult(bool Success, double Value);

/// <summary>
/// Problems define what parameters need to be tested to resolve fitness.
/// </summary>
public interface IProblem<TGenome>
	 where TGenome : IGenome
{
	int ID { get; }

	IReadOnlyList<IProblemPool<TGenome>> Pools { get; }

	IEnumerable<Fitness> ProcessSample(TGenome g, long sampleId);
	ValueTask<IEnumerable<Fitness>> ProcessSampleAsync(TGenome g, long sampleId);

	/// <summary>
	/// Optional per-case surface for task 25-0023's epsilon-lexicase ranking mode (see
	/// <see cref="CaseResult"/> and
	/// <see cref="Solve.ProcessingSchemes.RankingMode.EpsilonLexicase"/>). Returns
	/// one <see cref="CaseResult"/> per individual test case underlying the SAME evaluation
	/// <see cref="ProcessSampleAsync"/> would aggregate for <paramref name="sampleId"/> -- for
	/// Eater, one per grid sample in the level's sample set (see
	/// <c>Eater.Problem.ProcessSampleCasesAsync</c>) -- in a stable, sampleId-determined order,
	/// since <c>TowerScheme{TGenome}.RankByEpsilonLexicase</c> compares case index <c>k</c>
	/// across every entry in a cohort and only a shared, consistent case identity makes that
	/// comparison meaningful.
	/// </summary>
	/// <remarks>
	/// Defaults to returning <see langword="null"/>: a problem that does not override this opts
	/// out of lexicase ranking entirely. Selection falls back to the aggregate comparer for it
	/// even when <see cref="Solve.ProcessingSchemes.ISchemeConfig.RankingMode"/> is
	/// <see cref="Solve.ProcessingSchemes.RankingMode.EpsilonLexicase"/> -- see
	/// <c>TowerScheme{TGenome}.Level.RankEntries</c>, which checks every cohort entry's
	/// <c>LevelEntry.CaseResults</c> before attempting a lexicase pass. This keeps the default,
	/// aggregate-only path (and any problem that hasn't opted in) completely unaffected: no
	/// extra evaluation work is even scheduled unless <c>RankingMode</c> is explicitly set to
	/// <c>EpsilonLexicase</c> (see <c>TowerScheme.Level.ProcessContenderSafelyAsync</c>).
	/// </remarks>
	ValueTask<IReadOnlyList<CaseResult>?> ProcessSampleCasesAsync(TGenome g, long sampleId)
		=> new((IReadOnlyList<CaseResult>?)null);

	long TestCount { get; }

	bool HasConverged { get; }
	void Converged();

	public ImmutableArray<Fitness> NewFitness()
		=> Pools.Select(f => new Fitness(f.Metrics)).ToImmutableArray();
}
