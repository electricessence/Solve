using Eater;
using Solve.Metrics;
using Solve.ProcessingSchemes;
using System.Collections.Immutable;

namespace Solve.Tests;

/// <summary>
/// Coverage for 25-0024: the opt-in age-layered protection mechanism for the tower scheme.
/// </summary>
/// <remarks>
/// Three layers are exercised here, matching the acceptance criteria:
/// <list type="bullet">
/// <item>
/// <see cref="GenomeFactoryBase{TGenome}.AgeTrackingEnabled"/> /
/// <see cref="GenomeFactoryBase{TGenome}.GetAgeMarker"/>: the age marker itself, assigned at
/// registration, and provably zero-cost (no marker recorded at all) when disabled.
/// </item>
/// <item>
/// <see cref="SchemeConfig.AgeProtectionWindow"/>: the opt-in config surface, defaulting to
/// <see langword="null"/> (disabled).
/// </item>
/// <item>
/// <see cref="TowerScheme{TGenome}.TryPromoteBestYoungLoser"/>: the age-restricted ranking
/// lens itself -- the chosen mechanism (as opposed to a periodic protected regeneration
/// wave) -- proven against a hand-built cohort where every incumbent outscores a single
/// fresh genome on raw fitness.
/// </item>
/// </list>
/// </remarks>
public class TowerAgeProtectionTests
{
	private static GenomeFactory CreateFactory()
		=> new(new CounterRegistry(), seeds: null, leftTurnDisabled: true);

	// ------------------------------------------------------------------
	// SchemeConfig plumbing (acceptance criterion 2).
	// ------------------------------------------------------------------

	[Fact]
	public void AgeProtectionWindowDefaultsToNull()
	{
		var config = new SchemeConfig();
		Assert.Null(config.AgeProtectionWindow);
		Assert.Null(config.Immutable.AgeProtectionWindow);
	}

	[Fact]
	public void AgeProtectionWindowIsConfigurable()
	{
		var config = new SchemeConfig { AgeProtectionWindow = 500 };
		Assert.Equal(500, config.AgeProtectionWindow);
		Assert.Equal(500, config.Immutable.AgeProtectionWindow);
		Assert.Equal(500, config.Clone().AgeProtectionWindow);
	}

	// ------------------------------------------------------------------
	// GenomeFactoryBase age marker (acceptance criterion 1).
	// ------------------------------------------------------------------

	[Fact]
	public void AgeTrackingDisabledByDefaultAndAssignsNoMarkers()
	{
		GenomeFactory factory = CreateFactory();
		Assert.False(factory.AgeTrackingEnabled);

		Assert.True(factory.TryGenerateNew(out Genome? genome));
		Assert.NotNull(genome);

		// Zero cost when disabled: no marker was ever recorded for this genome, and the
		// counter never moved.
		Assert.Null(factory.GetAgeMarker(genome));
		Assert.Equal(0, factory.CurrentAgeCounter);
	}

	[Fact]
	public void AgeTrackingEnabledAssignsIncreasingMarkersOnceEachAtFirstRegistration()
	{
		GenomeFactory factory = CreateFactory();
		factory.AgeTrackingEnabled = true;

		Assert.True(factory.TryGenerateNew(out Genome? first));
		Assert.True(factory.TryGenerateNew(out Genome? second));
		Assert.True(factory.TryGenerateNew(out Genome? third));
		Assert.NotNull(first);
		Assert.NotNull(second);
		Assert.NotNull(third);

		long? m1 = factory.GetAgeMarker(first);
		long? m2 = factory.GetAgeMarker(second);
		long? m3 = factory.GetAgeMarker(third);

		Assert.NotNull(m1);
		Assert.NotNull(m2);
		Assert.NotNull(m3);

		// Registration order (first minted, first registered) is preserved: markers only
		// ever increase.
		Assert.True(m1 < m2, $"Expected {m1} < {m2} (registration order).");
		Assert.True(m2 < m3, $"Expected {m2} < {m3} (registration order).");

		// A genome already registered must never be reassigned a new marker.
		Assert.Equal(m1, factory.GetAgeMarker(first));
	}

	// ------------------------------------------------------------------
	// The age-restricted ranking lens itself (acceptance criterion 3).
	// ------------------------------------------------------------------

	private static LevelEntry<Genome> MakeEntry(Genome genome, double score)
	{
		ImmutableArray<double>[] scores = [ImmutableArray.Create(score)];
		var progress = new LevelProgress<Genome>(genome, ImmutableArray<Fitness>.Empty);
		// Not disposed: LossTracker.Dispose() recycles its InterlockedInt instances back
		// into a shared pool, which would be unsafe while this entry still references one.
		var losses = new LossTracker();
		return LevelEntry<Genome>.Init(progress, scores, losses[0]);
	}

	[Fact]
	public void AgeLensPromotesFreshGenomeThatWouldOtherwiseLose()
	{
		// Hand-built cohort: four incumbents that all comfortably outscore a single fresh
		// genome on raw fitness -- exactly the scenario acceptance criterion 3 describes.
		Genome genome = Genome.Parse("^");
		LevelEntry<Genome> freshEntry = MakeEntry(genome, score: 1);
		LevelEntry<Genome>[] cohort =
		[
			MakeEntry(genome, score: 100),
			MakeEntry(genome, score: 90),
			MakeEntry(genome, score: 80),
			MakeEntry(genome, score: 70),
			freshEntry,
		];
		int count = cohort.Length; // 5
		int midPoint = count / 2; // 2 -- matches ProcessSelection's own midPoint math.

		LevelEntry<Genome>[] ranked = (LevelEntry<Genome>[])cohort.Clone();
		Array.Sort(ranked, 0, count, LevelEntry<Genome>.GetScoreComparer(0));

		// Without age protection: the fresh genome ranks dead last on fitness alone, so it
		// lands in the loser half and would not be promoted this round.
		Assert.True(Array.IndexOf(ranked, freshEntry) >= midPoint,
			"Test setup invalid: the fresh entry must rank in the loser half without age protection.");

		// With age protection: it's the only (hence best-ranked) young genome among this
		// round's losers -- ranked against its own age-cohort only, per the "age-restricted
		// ranking lens" mechanism -- so it must be rescued into the winning half regardless
		// of how it compares to the established incumbents on raw fitness.
		var young = new HashSet<LevelEntry<Genome>> { freshEntry };
		bool rescued = TowerScheme<Genome>.TryPromoteBestYoungLoser(ranked, count, young);

		Assert.True(rescued);
		Assert.True(Array.IndexOf(ranked, freshEntry) < midPoint,
			"The fresh genome must survive into the promoted half once age-protected.");
	}

	[Fact]
	public void AgeLensRescuesOnlyTheBestRankedYoungLoser()
	{
		// Two young genomes among the losers: only the higher-fitness one of the two should
		// be rescued (one guaranteed survivor per pool per round), and it must be the
		// better-ranked of the pair, never the worse one.
		Genome genome = Genome.Parse("^");
		LevelEntry<Genome> betterYoung = MakeEntry(genome, score: 5);
		LevelEntry<Genome> worseYoung = MakeEntry(genome, score: 2);
		LevelEntry<Genome>[] cohort =
		[
			MakeEntry(genome, score: 100),
			MakeEntry(genome, score: 90),
			betterYoung,
			worseYoung,
		];
		int count = cohort.Length; // 4
		int midPoint = count / 2; // 2

		LevelEntry<Genome>[] ranked = (LevelEntry<Genome>[])cohort.Clone();
		Array.Sort(ranked, 0, count, LevelEntry<Genome>.GetScoreComparer(0));

		var young = new HashSet<LevelEntry<Genome>> { betterYoung, worseYoung };
		bool rescued = TowerScheme<Genome>.TryPromoteBestYoungLoser(ranked, count, young);

		Assert.True(rescued);
		Assert.True(Array.IndexOf(ranked, betterYoung) < midPoint,
			"The better-ranked young loser must be the one rescued.");
		Assert.True(Array.IndexOf(ranked, worseYoung) >= midPoint,
			"Only one young survivor is guaranteed per pool per round; the weaker one stays a loser.");
	}

	[Fact]
	public void AgeLensDoesNothingWhenNoYoungEntriesArePresent()
	{
		Genome genome = Genome.Parse("^");
		LevelEntry<Genome>[] cohort =
		[
			MakeEntry(genome, score: 100),
			MakeEntry(genome, score: 90),
			MakeEntry(genome, score: 80),
			MakeEntry(genome, score: 70),
		];
		int count = cohort.Length;

		LevelEntry<Genome>[] ranked = (LevelEntry<Genome>[])cohort.Clone();
		Array.Sort(ranked, 0, count, LevelEntry<Genome>.GetScoreComparer(0));
		LevelEntry<Genome>[] beforeRescue = (LevelEntry<Genome>[])ranked.Clone();

		bool rescued = TowerScheme<Genome>.TryPromoteBestYoungLoser(ranked, count, new HashSet<LevelEntry<Genome>>());

		Assert.False(rescued);
		Assert.Equal(beforeRescue, ranked);
	}

	[Fact]
	public void AgeLensDoesNotDisturbOrderWhenYoungGenomeAlreadyWins()
	{
		// A young genome that already ranks in the winning half on fitness merit alone
		// needs no rescue -- the lens must leave a cohort like this untouched.
		Genome genome = Genome.Parse("^");
		LevelEntry<Genome> youngWinner = MakeEntry(genome, score: 100);
		LevelEntry<Genome>[] cohort =
		[
			youngWinner,
			MakeEntry(genome, score: 90),
			MakeEntry(genome, score: 80),
			MakeEntry(genome, score: 70),
		];
		int count = cohort.Length;
		int midPoint = count / 2;

		LevelEntry<Genome>[] ranked = (LevelEntry<Genome>[])cohort.Clone();
		Array.Sort(ranked, 0, count, LevelEntry<Genome>.GetScoreComparer(0));

		var young = new HashSet<LevelEntry<Genome>> { youngWinner };
		bool rescued = TowerScheme<Genome>.TryPromoteBestYoungLoser(ranked, count, young);

		Assert.False(rescued);
		Assert.True(Array.IndexOf(ranked, youngWinner) < midPoint);
	}

	// ------------------------------------------------------------------
	// Full-pipeline composition smoke test: age protection must not destabilize the real
	// async tower pipeline (channels, shared worker pool, cancellation) it's wired into.
	// ------------------------------------------------------------------

	[Fact]
	public async Task AgeProtectionRunsCleanlyThroughTheFullPipeline()
	{
		// MaxConcurrentEvaluations is capped low deliberately: constructing many
		// TowerScheme instances at the default (Environment.ProcessorCount) each spins up
		// that many background evaluation workers, which was observed (during this same
		// task's development) to cause severe ThreadPool-starvation-class slowdowns across
		// the whole test suite when several such tests ran back-to-back. Real parallelism
		// isn't needed for a correctness/regression check like this one.
		GenomeFactory factory = CreateFactory();
		var scheme = new TowerScheme<Genome>(factory, new SchemeConfig
		{
			MaxLevels = 4,
			PoolSize = (8, 8, 0),
			MaxConcurrentEvaluations = 2,
			AgeProtectionWindow = 50,
		});
		var problem = Eater.Problem.CreateFitnessPrimary(6);
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
		Assert.True(problem.TestCount > 0, "No evaluations ran at all -- the pipeline stalled with age protection enabled.");

		// The Level constructor should have opted the factory into age tracking the moment
		// AgeProtectionWindow was set, so genomes minted during the run must have acquired
		// markers.
		Assert.True(factory.AgeTrackingEnabled);
		Assert.True(factory.CurrentAgeCounter > 0);
	}
}
