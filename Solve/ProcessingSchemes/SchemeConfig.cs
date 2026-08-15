using System.Diagnostics.Contracts;

namespace Solve.ProcessingSchemes;

public interface ISchemeConfig
{
	SchemeConfig.PoolSizing PoolSize { get; }
	ushort MaxLevels { get; }
	ushort MaxLevelLoss { get; }
	ushort MaxConsecutiveRejections { get; }
	ushort PercentRejectedBeforeElimination { get; }

	/// <summary>
	/// Maximum number of fitness evaluations the tower's shared worker stage will run
	/// concurrently. Bounds both CPU fan-out and the size of the in-flight evaluation queue.
	/// Defaults to <see cref="Environment.ProcessorCount"/>.
	/// </summary>
	int MaxConcurrentEvaluations { get; }

	/// <summary>
	/// Opt-in idle window for flushing a level's partially-filled pool through selection.
	/// Defaults to <see langword="null"/>, which preserves the scheme's original behavior
	/// exactly: a level's pool only runs selection once it fills to its full pool size, and a
	/// level sitting one (or more) entries short of that waits indefinitely once upstream
	/// inflow pauses. When set, a level whose pool is non-empty and has received no new entry
	/// for this window runs selection on the partial cohort instead of waiting further -- see
	/// <c>Level.RunPoolReaderWithIdleFlushAsync</c>. A cohort of exactly one entry is never
	/// flushed (selection requires at least one competitor), so it keeps waiting regardless of
	/// this setting.
	/// </summary>
	TimeSpan? IdleFlushAfter { get; }

	/// <summary>
	/// Opt-in age-layered protection: the number of most-recent genome registrations (see
	/// <see cref="GenomeFactoryBase{TGenome}.GetAgeMarker"/> and
	/// <see cref="GenomeFactoryBase{TGenome}.CurrentAgeCounter"/>) that count as "young" for
	/// the tower's per-selection age-restricted ranking lens.
	/// </summary>
	/// <remarks>
	/// Defaults to <see langword="null"/>, which disables the feature entirely and preserves
	/// today's selection behavior byte-for-byte: <c>TowerScheme{TGenome}.Level.RankEntries</c>
	/// runs only its original single fitness-only ranking pass (this option's presence isn't
	/// even consulted), and <c>GenomeFactoryBase{TGenome}.AgeTrackingEnabled</c> is never
	/// switched on, so genome registration performs zero additional work either.
	/// <para>
	/// When set, each level's per-pool selection cohort is still ranked by fitness exactly as
	/// before, but the single best-ranked "young" genome (age marker within this many of the
	/// factory's current age counter) among that round's fitness losers -- ranked only against
	/// other young genomes, i.e. through a lens restricted to its own age-cohort, never
	/// directly against established ones -- is guaranteed to survive into the winning half of
	/// that round regardless of how it compares to established genomes on raw fitness. This is
	/// the "age-restricted ranking lens" mechanism (as opposed to a periodic protected
	/// regeneration wave): it composes naturally with the existing per-cohort selection call
	/// this scheme already performs, needs no new timer/scheduling infrastructure, and its
	/// effect is scoped per level exactly like every other selection rule already is.
	/// </para>
	/// </remarks>
	int? AgeProtectionWindow { get; }

	/// <summary>
	/// Selects how a level's per-pool cohort is ordered for selection (task 25-0023). Defaults
	/// to <see cref="ProcessingSchemes.RankingMode.Aggregate"/>, which preserves today's
	/// selection behavior byte-for-byte: <c>TowerScheme{TGenome}.Level.RankEntries</c> sorts
	/// each pool's cohort by <see cref="ProcessingSchemes.LevelEntry{TGenome}.GetScoreComparer"/>
	/// exactly as it always has, and no per-case evaluation work is ever scheduled (see
	/// <c>TowerScheme.Level.ProcessContenderSafelyAsync</c>, which only calls
	/// <see cref="IProblem{TGenome}.ProcessSampleCasesAsync"/> when this is
	/// <see cref="ProcessingSchemes.RankingMode.EpsilonLexicase"/>). Set to
	/// <see cref="ProcessingSchemes.RankingMode.EpsilonLexicase"/> to rank cohorts via
	/// <c>TowerScheme{TGenome}.RankByEpsilonLexicase</c> instead -- see that method and
	/// <see cref="CaseResult"/> for the algorithm and per-case data contract.
	/// </summary>
	RankingMode RankingMode { get; }

	SchemeConfig.Values Immutable { get; }
}

/// <summary>
/// Selects how a level orders a selection cohort (task 25-0023). See
/// <see cref="ISchemeConfig.RankingMode"/> for the full contract. Deliberately NOT nested
/// inside <see cref="SchemeConfig"/> (unlike e.g. <see cref="SchemeConfig.PoolSizing"/>):
/// <see cref="ISchemeConfig.RankingMode"/> and this type intentionally share one name -- the
/// idiomatic "a property named after its own enum type" pairing -- which a nested declaration
/// would make illegal (a type cannot contain a nested type and a member with the same
/// identifier).
/// </summary>
public enum RankingMode
{
	/// <summary>
	/// Rank each pool's cohort by <see cref="ProcessingSchemes.LevelEntry{TGenome}.GetScoreComparer"/>
	/// over that pool's aggregate fitness -- the scheme's original, only-ever behavior.
	/// </summary>
	Aggregate = 0,

	/// <summary>
	/// Rank cohorts via <c>TowerScheme{TGenome}.RankByEpsilonLexicase</c> over per-case results
	/// (<see cref="CaseResult"/>) instead. Requires the active <see cref="ISchemeConfig"/>'s
	/// <see cref="IProblem{TGenome}"/> to override <see cref="IProblem{TGenome}.ProcessSampleCasesAsync"/>;
	/// a problem that doesn't falls back to <see cref="Aggregate"/> transparently (see that
	/// method's remarks).
	/// </summary>
	EpsilonLexicase = 1,
}

#pragma warning disable IDE0079 // Remove unnecessary suppression
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2225:Operator overloads have named alternates")]
#pragma warning restore IDE0079 // Remove unnecessary suppression
public class SchemeConfig : ISchemeConfig
{
	public readonly record struct Values : ISchemeConfig
	{
		public Values(
			PoolSizing poolSize,
			ushort maxLevels,
			ushort maxLevelLoss,
			ushort maxConsecutiveRejections,
			ushort percentRejectedBeforeElimination,
			int maxConcurrentEvaluations,
			TimeSpan? idleFlushAfter = null,
			int? ageProtectionWindow = null,
			RankingMode rankingMode = RankingMode.Aggregate)
		{
			PoolSize = poolSize;
			MaxLevels = maxLevels;
			MaxLevelLoss = maxLevelLoss;
			MaxConsecutiveRejections = maxConsecutiveRejections;
			PercentRejectedBeforeElimination = percentRejectedBeforeElimination;
			MaxConcurrentEvaluations = maxConcurrentEvaluations;
			IdleFlushAfter = idleFlushAfter;
			AgeProtectionWindow = ageProtectionWindow;
			RankingMode = rankingMode;
		}

		public PoolSizing PoolSize { get; }
		public ushort MaxLevels { get; }
		public ushort MaxLevelLoss { get; }
		public ushort MaxConsecutiveRejections { get; }
		public ushort PercentRejectedBeforeElimination { get; }
		public int MaxConcurrentEvaluations { get; }
		public TimeSpan? IdleFlushAfter { get; }
		public int? AgeProtectionWindow { get; }
		public RankingMode RankingMode { get; }

		public Values Immutable => this;
	}

	public readonly record struct PoolSizing
	{
		private const string MUST_BE_MULTIPLE_OF_2 = "Must be a mutliple of 2.";

		public PoolSizing(ushort first, ushort minimum, ushort step)
		{
			if (minimum < 2)
				throw new ArgumentOutOfRangeException(nameof(minimum), "Must be at least 2.");
			if (first % 2 == 1)
				throw new ArgumentException(MUST_BE_MULTIPLE_OF_2, nameof(first));
			if (minimum % 2 == 1)
				throw new ArgumentException(MUST_BE_MULTIPLE_OF_2, nameof(minimum));
			if (step % 2 == 1)
				throw new ArgumentException(MUST_BE_MULTIPLE_OF_2, nameof(step));
			if (first < minimum)
				throw new ArgumentException("Minumum must be less than or equal to First.", nameof(minimum));
			Contract.EndContractBlock();

			First = first;
			Minimum = minimum;
			Step = step;
		}

		public PoolSizing((ushort First, ushort Minimum, ushort Step) values)
			: this(values.First, values.Minimum, values.Step) { }

		public void Deconstruct(out ushort first, out ushort minimum, out ushort step)
		{
			first = First;
			minimum = Minimum;
			step = Step;
		}

		public ushort First { get; }
		public ushort Minimum { get; }
		public ushort Step { get; }

		public static implicit operator PoolSizing((ushort First, ushort Minimum, ushort Step) values)
			=> new(values);

		public static implicit operator (ushort First, ushort Minimum, ushort Step)(PoolSizing sizing)
			=> (sizing.First, sizing.Minimum, sizing.Step);

		public ushort GetPoolSize(int level)
		{
			(ushort First, ushort Minimum, ushort Step) = this;
			int maxDelta = First - Minimum;
			int decrement = level * Step;
			return decrement > maxDelta ? Minimum : (ushort)(First - decrement);
		}
	}

	public PoolSizing PoolSize { get; init; }
	public ushort MaxLevels { get; init; } = ushort.MaxValue;
	public ushort MaxLevelLoss { get; init; } = 3;
	public ushort MaxConsecutiveRejections { get; init; } = 10;

	public ushort PercentRejectedBeforeElimination { get; init; } = 70;

	/// <inheritdoc cref="ISchemeConfig.MaxConcurrentEvaluations"/>
	public int MaxConcurrentEvaluations { get; init; } = Environment.ProcessorCount;

	/// <inheritdoc cref="ISchemeConfig.IdleFlushAfter"/>
	public TimeSpan? IdleFlushAfter { get; init; }

	/// <inheritdoc cref="ISchemeConfig.AgeProtectionWindow"/>
	public int? AgeProtectionWindow { get; init; }

	/// <inheritdoc cref="ISchemeConfig.RankingMode"/>
	public RankingMode RankingMode { get; init; } = RankingMode.Aggregate;

	public static implicit operator Values(SchemeConfig config) => config.Immutable;

	public SchemeConfig Clone() => new()
	{
		PoolSize = PoolSize,
		MaxLevels = MaxLevels,
		MaxLevelLoss = MaxLevelLoss,
		MaxConsecutiveRejections = MaxConsecutiveRejections,
		PercentRejectedBeforeElimination = PercentRejectedBeforeElimination,
		MaxConcurrentEvaluations = MaxConcurrentEvaluations,
		IdleFlushAfter = IdleFlushAfter,
		AgeProtectionWindow = AgeProtectionWindow,
		RankingMode = RankingMode
	};

	public Values Immutable => new(PoolSize, MaxLevels, MaxLevelLoss, MaxConsecutiveRejections, PercentRejectedBeforeElimination, MaxConcurrentEvaluations, IdleFlushAfter, AgeProtectionWindow, RankingMode);
}
