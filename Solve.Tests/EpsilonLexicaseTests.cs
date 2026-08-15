using Eater;
using Solve.Metrics;
using Solve.ProcessingSchemes;
using System.Collections.Immutable;

namespace Solve.Tests;

/// <summary>
/// Coverage for task 25-0023: the opt-in epsilon-lexicase level-ranking mode.
/// </summary>
/// <remarks>
/// Matches this task's acceptance criteria:
/// <list type="bullet">
/// <item>
/// <see cref="SchemeConfig.RankingMode"/>: the opt-in config surface, defaulting to
/// <see cref="RankingMode.Aggregate"/> (criterion 2).
/// </item>
/// <item>
/// <see cref="TowerScheme{TGenome}.RankByEpsilonLexicase"/>: the ranking routine itself,
/// proven against a hand-built cohort where a specialist genome (best on two of three cases,
/// but dragged to the bottom of an aggregate sum-based ranking by one bad case) survives into
/// the top half under lexicase but not under the aggregate comparer -- and that the routine is
/// deterministic given the same seed (criterion 4).
/// </item>
/// <item>
/// A full-pipeline smoke test proving <see cref="RankingMode.EpsilonLexicase"/> composes
/// cleanly with the real async tower (channels, shared worker pool, cancellation,
/// <c>ProcessContenderSafelyAsync</c>'s conditional <c>ProcessSampleCasesAsync</c> call, and
/// <c>Level.RankEntries</c>'s lexicase branch) exactly as <c>TowerAgeProtectionTests</c> does
/// for task 25-0024's age-protection lens.
/// </item>
/// </list>
/// </remarks>
public class EpsilonLexicaseTests
{
	// ------------------------------------------------------------------
	// SchemeConfig plumbing (acceptance criterion 2).
	// ------------------------------------------------------------------

	[Fact]
	public void RankingModeDefaultsToAggregate()
	{
		var config = new SchemeConfig();
		Assert.Equal(RankingMode.Aggregate, config.RankingMode);
		Assert.Equal(RankingMode.Aggregate, config.Immutable.RankingMode);
	}

	[Fact]
	public void RankingModeIsConfigurable()
	{
		var config = new SchemeConfig { RankingMode = RankingMode.EpsilonLexicase };
		Assert.Equal(RankingMode.EpsilonLexicase, config.RankingMode);
		Assert.Equal(RankingMode.EpsilonLexicase, config.Immutable.RankingMode);
		Assert.Equal(RankingMode.EpsilonLexicase, config.Clone().RankingMode);
	}

	// ------------------------------------------------------------------
	// IProblem per-case surface (acceptance criterion 1): default opt-out, Eater's opt-in.
	// ------------------------------------------------------------------

	[Fact]
	public async Task EaterOverrideIsReachedThroughInterfaceDispatch()
	{
		// Eater opts in to the per-case surface (see Eater.Problem.ProcessSampleCasesAsync).
		// Calling through the IProblem<TGenome> interface reference -- exactly how
		// TowerScheme.Level.ProcessContenderSafelyAsync calls it -- must reach that override,
		// not ProblemBase's "return null" default. This is the exact wiring
		// ProblemBase.ProcessSampleCasesAsync's remarks explain is NOT automatic for a
		// same-signature method on a subclass without a matching virtual member on the base
		// class that first implements IProblem<TGenome>.
		IProblem<Genome> problem = Eater.Problem.CreateFitnessPrimary(6);
		IReadOnlyList<CaseResult>? cases = await problem.ProcessSampleCasesAsync(Genome.Parse("^"), sampleId: 0);

		Assert.NotNull(cases);
		Assert.NotEmpty(cases!);
	}

	// ------------------------------------------------------------------
	// The lexicase routine itself (acceptance criteria 3 and 4).
	// ------------------------------------------------------------------

	private static LevelEntry<Genome> MakeEntry(double aggregateSum, params CaseResult[] cases)
	{
		Genome genome = Genome.Parse("^");
		var progress = new LevelProgress<Genome>(genome, ImmutableArray<Fitness>.Empty);
		// Not disposed: LossTracker.Dispose() recycles its InterlockedInt instances back into a
		// shared pool, which would be unsafe while this entry still references one (same
		// rationale as TowerAgeProtectionTests.MakeEntry).
		var losses = new LossTracker();
		ImmutableArray<double>[] scores = [ImmutableArray.Create(aggregateSum)];
		return LevelEntry<Genome>.Init(progress, scores, losses[0], won: false, caseResults: cases);
	}

	/// <summary>
	/// Cohort for the specialist-vs-generalists scenario acceptance criterion 4 describes:
	/// three "generalist" entries (G1-G3) with a consistent, moderate value on all three cases,
	/// and one "specialist" (S) that dominates two of the three cases by a wide margin but does
	/// badly on the third -- pulling its SUM (matching production's aggregate convention: raw
	/// per-level fitness <em>sums</em>, not averages -- see
	/// <c>TowerScheme{TGenome}.Level.ProcessContenderSafelyAsync</c>'s <c>levelFitness</c>)
	/// below every generalist's total.
	/// </summary>
	private static (LevelEntry<Genome>[] Cohort, LevelEntry<Genome> Specialist) BuildSpecialistCohort()
	{
		LevelEntry<Genome> g1 = MakeEntry(-15, new CaseResult(true, -5), new CaseResult(true, -5), new CaseResult(true, -5));
		LevelEntry<Genome> g2 = MakeEntry(-15, new CaseResult(true, -6), new CaseResult(true, -4), new CaseResult(true, -5));
		LevelEntry<Genome> g3 = MakeEntry(-15, new CaseResult(true, -4), new CaseResult(true, -6), new CaseResult(true, -5));
		LevelEntry<Genome> s = MakeEntry(-20, new CaseResult(true, 0), new CaseResult(true, 0), new CaseResult(true, -20));
		return ([g1, g2, g3, s], s);
	}

	[Fact]
	public void AggregateRankingPlacesTheSpecialistInTheBottomHalf()
	{
		(LevelEntry<Genome>[] cohort, LevelEntry<Genome> specialist) = BuildSpecialistCohort();
		int count = cohort.Length; // 4
		int midPoint = count / 2; // 2

		LevelEntry<Genome>[] ranked = (LevelEntry<Genome>[])cohort.Clone();
		Array.Sort(ranked, 0, count, LevelEntry<Genome>.GetScoreComparer(0));

		// Test setup sanity: the specialist's total (-20) is strictly worse than every
		// generalist's (-15), so the aggregate comparer must place it dead last.
		Assert.True(Array.IndexOf(ranked, specialist) >= midPoint,
			"Test setup invalid: the specialist must rank in the loser half under aggregate ranking.");
	}

	[Fact]
	public void LexicaseOrderingPromotesTheSpecialistIntoTheTopHalf()
	{
		(LevelEntry<Genome>[] cohort, LevelEntry<Genome> specialist) = BuildSpecialistCohort();
		int count = cohort.Length;
		int midPoint = count / 2;

		LevelEntry<Genome>[] ranked = (LevelEntry<Genome>[])cohort.Clone();
		// Seed chosen (see task completion notes) so this case order draws one of the
		// specialist's two dominant cases first -- at which point it's the sole survivor of
		// that case's epsilon-MAD filter and the routine short-circuits with it alone at rank
		// 0, before the case it does badly on is ever reached.
		TowerScheme<Genome>.RankByEpsilonLexicase(ranked, count, new Random(0));

		Assert.True(Array.IndexOf(ranked, specialist) < midPoint,
			"The specialist must survive lexicase ordering into the top half despite ranking " +
			"bottom-half under aggregate ranking (see AggregateRankingPlacesTheSpecialistInTheBottomHalf).");
	}

	[Fact]
	public void RankingIsDeterministicUnderTheSameSeed()
	{
		(LevelEntry<Genome>[] cohort, _) = BuildSpecialistCohort();
		int count = cohort.Length;

		LevelEntry<Genome>[] a = (LevelEntry<Genome>[])cohort.Clone();
		LevelEntry<Genome>[] b = (LevelEntry<Genome>[])cohort.Clone();

		// Two independently-seeded Random instances with the same seed must shuffle case order
		// identically and so produce a bit-identical resulting ordering.
		TowerScheme<Genome>.RankByEpsilonLexicase(a, count, new Random(7));
		TowerScheme<Genome>.RankByEpsilonLexicase(b, count, new Random(7));

		Assert.Equal(a, b);
	}

	[Fact]
	public void DifferentSeedsCanProduceDifferentOrderings()
	{
		// Sanity check that case order genuinely drives the outcome (not a coincidence of this
		// particular cohort): across a spread of seeds, the specialist's resulting rank must
		// vary, proving the random case order actually matters to the routine's output.
		(LevelEntry<Genome>[] cohort, LevelEntry<Genome> specialist) = BuildSpecialistCohort();
		int count = cohort.Length;

		var observedRanks = new HashSet<int>();
		for (int seed = 0; seed < 25; seed++)
		{
			LevelEntry<Genome>[] working = (LevelEntry<Genome>[])cohort.Clone();
			TowerScheme<Genome>.RankByEpsilonLexicase(working, count, new Random(seed));
			observedRanks.Add(Array.IndexOf(working, specialist));
		}

		Assert.True(observedRanks.Count > 1,
			"Expected the specialist's rank to vary across different seeds' case orders.");
	}

	[Fact]
	public void SuccessIsAHardGateNotAnEpsilonToleratedOne()
	{
		// A single case: three entries succeed with a wide spread of Values, one entry fails
		// outright with the numerically best Value. Success must dominate regardless of Value
		// -- the failing entry must never be treated as "close enough" to the successes. The
		// three successes share an identical Value (zero spread, so stage 2's own MAD-epsilon
		// filter -- a separate concern this test isn't targeting -- keeps all three of them
		// too), isolating stage 1's hard gate as this case's only source of elimination.
		LevelEntry<Genome> winnerA = MakeEntry(0, new CaseResult(true, -50));
		LevelEntry<Genome> winnerB = MakeEntry(0, new CaseResult(true, -50));
		LevelEntry<Genome> winnerC = MakeEntry(0, new CaseResult(true, -50));
		LevelEntry<Genome> failedButNumericallyBest = MakeEntry(0, new CaseResult(false, 1000));

		LevelEntry<Genome>[] cohort = [winnerA, winnerB, winnerC, failedButNumericallyBest];
		TowerScheme<Genome>.RankByEpsilonLexicase(cohort, cohort.Length, new Random(1));

		Assert.Equal(3, Array.IndexOf(cohort, failedButNumericallyBest));
	}

	[Fact]
	public void SingleEntryCohortIsLeftUnchanged()
	{
		LevelEntry<Genome> only = MakeEntry(0, new CaseResult(true, 1));
		LevelEntry<Genome>[] cohort = [only];

		// Must not throw for a degenerate one-entry "cohort" (mirrors
		// TryPromoteBestYoungLoser's count < 2 guard).
		TowerScheme<Genome>.RankByEpsilonLexicase(cohort, 1, new Random(0));

		Assert.Same(only, cohort[0]);
	}

	[Fact]
	public void EntriesWithoutCaseResultsAreLeftInOriginalOrder()
	{
		// LevelEntry.CaseResults is null unless RankingMode was EpsilonLexicase AND the entry
		// was actually pooled -- RankByEpsilonLexicase itself (called directly, bypassing
		// Level.RankEntries's HasCaseResultsThroughout guard) must still behave safely: with no
		// case data on the very first entry it takes as authoritative for case count, it treats
		// the cohort as having zero cases and leaves order untouched rather than throwing.
		Genome genome = Genome.Parse("^");
		var progress1 = new LevelProgress<Genome>(genome, ImmutableArray<Fitness>.Empty);
		var progress2 = new LevelProgress<Genome>(genome, ImmutableArray<Fitness>.Empty);
		LevelEntry<Genome> a = LevelEntry<Genome>.Init(progress1, [ImmutableArray.Create(1d)], new LossTracker()[0]);
		LevelEntry<Genome> b = LevelEntry<Genome>.Init(progress2, [ImmutableArray.Create(2d)], new LossTracker()[0]);
		LevelEntry<Genome>[] cohort = [a, b];

		TowerScheme<Genome>.RankByEpsilonLexicase(cohort, 2, new Random(0));

		Assert.Same(a, cohort[0]);
		Assert.Same(b, cohort[1]);
	}

	// ------------------------------------------------------------------
	// Full-pipeline composition smoke test: EpsilonLexicase must not destabilize the real
	// async tower pipeline (channels, shared worker pool, cancellation) it's wired into, and
	// must compose with the per-case fetch in ProcessContenderSafelyAsync and the lexicase
	// branch in Level.RankEntries.
	// ------------------------------------------------------------------

	private static GenomeFactory CreateFactory()
		=> new(new CounterRegistry(), seeds: null, leftTurnDisabled: true);

	[Fact]
	public async Task EpsilonLexicaseRunsCleanlyThroughTheFullPipeline()
	{
		// MaxConcurrentEvaluations capped low deliberately -- see TowerAgeProtectionTests'
		// identical rationale: constructing many TowerScheme instances at the default degree of
		// parallelism was observed to thread-pool-starve the whole suite.
		GenomeFactory factory = CreateFactory();
		var scheme = new TowerScheme<Genome>(factory, new SchemeConfig
		{
			MaxLevels = 4,
			PoolSize = (8, 8, 0),
			MaxConcurrentEvaluations = 2,
			RankingMode = RankingMode.EpsilonLexicase,
		});
		Eater.Problem problem = Eater.Problem.CreateFitnessPrimary(6);
		scheme.AddProblem(problem);

		Exception? observed = null;
		scheme.Subscribe(_ => { }, ex => observed = ex);

		Task run = scheme.Start();
		await Task.Delay(TimeSpan.FromSeconds(2));

		scheme.Cancel();
		try { await run.WaitAsync(TimeSpan.FromSeconds(10)); }
		catch (OperationCanceledException) { }
		catch (TimeoutException) { }

		Assert.Null(observed);
		Assert.True(problem.TestCount > 0, "No evaluations ran at all -- the pipeline stalled with EpsilonLexicase ranking enabled.");
	}
}
