using Open.ChannelExtensions;
using Open.Disposable;
using Open.Memory;
using Solve.Telemetry;
using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Solve.ProcessingSchemes;

public partial class TowerScheme<TGenome>
{
	/// <summary>
	/// Core step of the opt-in age-protection "ranking lens" (see
	/// <see cref="SchemeConfig.AgeProtectionWindow"/>): given a pool's cohort already sorted
	/// descending by fitness into <paramref name="ranked"/>[0, <paramref name="count"/>), finds
	/// the best-ranked entry in the loser half (<c>[count/2, count)</c>) that belongs to
	/// <paramref name="young"/> and swaps it into the last winning slot so it survives this
	/// selection round regardless of how it compares to established genomes on raw fitness.
	/// </summary>
	/// <remarks>
	/// Scanning the loser half from its start (index <c>count/2</c>) upward and stopping at the
	/// first match is what makes this "young genomes compared only against their cohort": since
	/// <paramref name="ranked"/> is already a total order by fitness, restricting that same
	/// order to just the entries in <paramref name="young"/> preserves their relative ranking —
	/// so the first young entry found this way is, by construction, the highest-fitness young
	/// genome among this round's losers, chosen without ever comparing it directly against an
	/// established genome. At most one entry is rescued per call (one guaranteed young survivor
	/// per pool, per selection round) — <see cref="Level.RankEntries"/> calls this once per pool
	/// lens.
	/// <para>
	/// Declared <see langword="public static"/> (rather than nested in the otherwise-protected
	/// <see cref="Level"/> class) specifically so this pure, allocation-free step is
	/// independently unit-testable against a hand-built cohort without needing to stand up the
	/// tower's channel/worker infrastructure.
	/// </para>
	/// </remarks>
	/// <returns><see langword="true"/> if an entry was rescued; otherwise <see langword="false"/>.</returns>
	public static bool TryPromoteBestYoungLoser(
		LevelEntry<TGenome>[] ranked,
		int count,
		IReadOnlySet<LevelEntry<TGenome>> young)
	{
		ArgumentNullException.ThrowIfNull(ranked);
		ArgumentNullException.ThrowIfNull(young);
		if (count < 2 || young.Count == 0) return false;

		int midPoint = count / 2;
		for (int j = midPoint; j < count; j++)
		{
			if (!young.Contains(ranked[j])) continue;
			(ranked[midPoint - 1], ranked[j]) = (ranked[j], ranked[midPoint - 1]);
			return true;
		}

		return false;
	}

	/// <summary>
	/// Task 25-0023's epsilon-lexicase ranking mode: orders <paramref name="entries"/>[0,
	/// <paramref name="count"/>) in place, best first, for use wherever
	/// <see cref="ISchemeConfig.RankingMode"/> is
	/// <see cref="RankingMode.EpsilonLexicase"/> instead of
	/// <see cref="LevelEntry{TGenome}.GetScoreComparer"/>'s aggregate sort (see
	/// <see cref="Level.RankEntries"/>).
	/// </summary>
	/// <remarks>
	/// <para>
	/// Every entry in <paramref name="entries"/>[0, <paramref name="count"/>) must carry the
	/// same number of <see cref="LevelEntry{TGenome}.CaseResults"/> -- a caller checks this
	/// (see <see cref="Level.RankEntries"/>) before calling; this method itself only reads
	/// <c>entries[0].CaseResults</c>'s length as the case count, and returns without altering
	/// order at all if that's zero. Cases are visited in an order freshly shuffled from
	/// <paramref name="random"/> every call ("random case order per selection event" --
	/// acceptance criterion 3 of task 25-0023): starting from the full cohort as "survivors,"
	/// each case in turn narrows the survivor set via two nested filters:
	/// </para>
	/// <list type="number">
	/// <item>
	/// A hard, non-epsilon-tolerant gate on <see cref="CaseResult.Success"/>: if any survivor
	/// succeeded on this case, every survivor that didn't is eliminated outright this round.
	/// Success is categorical (not a continuous error term), so there is no meaningful "close to
	/// succeeding" -- unlike <see cref="CaseResult.Value"/> below, no epsilon slack ever crosses
	/// this boundary.
	/// </item>
	/// <item>
	/// Among the survivors of gate 1 (all sharing this case's <see cref="CaseResult.Success"/>
	/// outcome), an epsilon-tolerant filter on <see cref="CaseResult.Value"/> (higher is
	/// better): epsilon is this case's survivor spread -- the median absolute deviation (MAD) of
	/// their <see cref="CaseResult.Value"/>s, the "documented equivalent" acceptance criterion 3
	/// allows in place of MAD -- and every survivor within epsilon of the best (maximum) value
	/// on this case is kept.
	/// </item>
	/// </list>
	/// <para>
	/// Entries eliminated by the same case's filters form one elimination round. Once a case's
	/// filters leave at most one survivor, remaining cases are skipped (nothing left to
	/// discriminate between). The final order places whichever entries were never eliminated
	/// (survived every case that ran) first, then each elimination round in reverse order --
	/// entries eliminated at a later case rank ahead of entries eliminated earlier ("survivors
	/// of the final case ranked ahead of earlier eliminations" -- acceptance criterion 3).
	/// Entries within the same round keep their relative order from the incoming
	/// <paramref name="entries"/> (a stable, deterministic tie-break). The result is a full
	/// ordering of the cohort -- not a single lexicase-selected winner -- ready for
	/// <see cref="Level.ProcessSelection"/>'s existing top-half promotion logic exactly like an
	/// aggregate-sorted cohort would be.
	/// </para>
	/// </remarks>
	public static void RankByEpsilonLexicase(
		LevelEntry<TGenome>[] entries,
		int count,
		Random random)
	{
		ArgumentNullException.ThrowIfNull(entries);
		ArgumentNullException.ThrowIfNull(random);
		if (count < 2) return;

		int caseCount = entries[0].CaseResults?.Count ?? 0;
		if (caseCount == 0) return;

		// Random case order for this selection event (Fisher-Yates).
		var caseOrder = new int[caseCount];
		for (int i = 0; i < caseCount; i++) caseOrder[i] = i;
		for (int i = caseCount - 1; i > 0; i--)
		{
			int j = random.Next(i + 1);
			(caseOrder[i], caseOrder[j]) = (caseOrder[j], caseOrder[i]);
		}

		// Snapshot the incoming order and work with indices into it so `entries` isn't mutated
		// until the final order is fully known.
		var snapshot = new LevelEntry<TGenome>[count];
		Array.Copy(entries, snapshot, count);

		var survivors = new List<int>(count);
		for (int i = 0; i < count; i++) survivors.Add(i);

		// Elimination rounds in the order cases eliminated them (earliest first); reversed
		// when building the final order below.
		var eliminationRounds = new List<List<int>>();

		foreach (int caseIndex in caseOrder)
		{
			if (survivors.Count <= 1) break;

			bool anySuccess = false;
			foreach (int idx in survivors)
			{
				if (snapshot[idx].CaseResults![caseIndex].Success) { anySuccess = true; break; }
			}

			List<int> stage1Survivors;
			List<int>? stage1Eliminated = null;
			if (anySuccess)
			{
				stage1Survivors = new List<int>(survivors.Count);
				foreach (int idx in survivors)
				{
					if (snapshot[idx].CaseResults![caseIndex].Success)
						stage1Survivors.Add(idx);
					else
						(stage1Eliminated ??= new List<int>()).Add(idx);
				}
			}
			else
			{
				stage1Survivors = survivors;
			}

			List<int> stage2Survivors = stage1Survivors;
			List<int>? stage2Eliminated = null;
			if (stage1Survivors.Count > 1)
			{
				var values = new double[stage1Survivors.Count];
				for (int k = 0; k < stage1Survivors.Count; k++)
					values[k] = snapshot[stage1Survivors[k]].CaseResults![caseIndex].Value;

				double best = values.Max();
				double epsilon = MedianAbsoluteDeviation(values);
				double threshold = best - epsilon;

				stage2Survivors = new List<int>(stage1Survivors.Count);
				for (int k = 0; k < stage1Survivors.Count; k++)
				{
					int idx = stage1Survivors[k];
					if (values[k] >= threshold)
						stage2Survivors.Add(idx);
					else
						(stage2Eliminated ??= new List<int>()).Add(idx);
				}
			}

			if (stage1Eliminated is not null || stage2Eliminated is not null)
			{
				var eliminated = new List<int>(
					(stage1Eliminated?.Count ?? 0) + (stage2Eliminated?.Count ?? 0));
				if (stage1Eliminated is not null) eliminated.AddRange(stage1Eliminated);
				if (stage2Eliminated is not null) eliminated.AddRange(stage2Eliminated);
				eliminationRounds.Add(eliminated);
			}

			survivors = stage2Survivors;
		}

		int w = 0;
		foreach (int idx in survivors) entries[w++] = snapshot[idx];
		for (int r = eliminationRounds.Count - 1; r >= 0; r--)
		{
			foreach (int idx in eliminationRounds[r]) entries[w++] = snapshot[idx];
		}

		Debug.Assert(w == count);
	}

	/// <summary>
	/// The median absolute deviation (MAD) of <paramref name="values"/> -- the median of each
	/// value's absolute distance from the set's own median. Used by
	/// <see cref="RankByEpsilonLexicase"/> as this case's epsilon: a robust (outlier-resistant,
	/// unlike a mean-based standard deviation) measure of the current survivors' spread on this
	/// case's <see cref="CaseResult.Value"/>.
	/// </summary>
	private static double MedianAbsoluteDeviation(double[] values)
	{
		Debug.Assert(values.Length > 0);
		double median = Median(values);
		var deviations = new double[values.Length];
		for (int i = 0; i < values.Length; i++)
			deviations[i] = Math.Abs(values[i] - median);
		return Median(deviations);
	}

	private static double Median(double[] values)
	{
		// Small groups only (a case's current survivor count within one selection event) -- a
		// full sort is simplest and cheap at this scale, avoiding a dependency on a
		// partial-selection algorithm for what's already a bounded, per-selection-event cost.
		var sorted = (double[])values.Clone();
		Array.Sort(sorted);
		int n = sorted.Length;
		return n % 2 == 1
			? sorted[n / 2]
			: (sorted[(n / 2) - 1] + sorted[n / 2]) / 2.0;
	}

	protected class Level : IAsyncLevel<TGenome>
	{
		protected bool IsTop => !_nextLevel.IsValueCreated;
		protected readonly bool IsMax;

		protected readonly int Index;
		protected readonly ushort PoolSize;
		protected readonly ProblemTower Tower;
		private readonly Lazy<IAsyncLevel<TGenome>> _nextLevel;
		public IAsyncLevel<TGenome> NextLevel => _nextLevel.Value;

		protected readonly Memory<double[]> BestLevelFitness;
		protected readonly Memory<double[]> BestProgressiveFitness;

		protected readonly Channel<LevelEntry<TGenome>> Pool;

		// 25-0024: cached once, reused by every RankEntries call at this level (see the
		// AgeProtectionWindow branch there). Null whenever age protection is disabled
		// (AgeProtectionWindow is null -- the cast is skipped entirely, see the
		// constructor below) or Tower.Factory is not a GenomeFactoryBase (age markers are
		// only ever recorded there; a plain IGenomeFactory<TGenome> implementation has no
		// way to supply them, so the lens has no effect for it).
		private readonly GenomeFactoryBase<TGenome>? _ageSource;
		private readonly int? _ageProtectionWindow;

		// Reused across RankEntries calls when age protection is enabled -- see
		// RankEntries for why this must never allocate on the disabled path.
		private HashSet<LevelEntry<TGenome>>? _youngScratch;

		// Reused across RankEntries calls when epsilon-lexicase ranking is enabled (25-0023) --
		// lazily sized on first use, mirroring _youngScratch above, so RankingMode's default
		// (Aggregate) path never allocates this buffer at all.
		private LevelEntry<TGenome>[]? _lexicaseScratch;

			// 15-0030: backing state for the solve.tower.level_count observable gauge -- see
			// EngineInstruments' type-level remarks for why this is weakly keyed and pull-based.
			// Keyed by ProblemTower rather than kept as an instance field on Level, because the
			// gauge reports one value per *tower*, not per Level: every Level of a given tower
			// shares the same box, updated as each successive level is created. Levels for one
			// tower are always created in strictly increasing index order and at most one at a
			// time (CreateNextLevel is only ever invoked through the owning
			// Lazy<IAsyncLevel<TGenome>>, which serializes concurrent first-access), so a plain
			// (non-Interlocked) write to the box below is safe.
			private static readonly ConditionalWeakTable<ProblemTower, StrongBox<long>> _levelCounts = new();

		protected virtual IAsyncLevel<TGenome> CreateNextLevel()
			=> new Level(Index + 1, Tower);

		public Level(
			int level,
			ProblemTower tower)
		{
			Debug.Assert(level >= 0);
			Debug.Assert(tower is not null);
			SchemeConfig.Values config = tower.Config;

			// Validate BEFORE any side effects: no level index at or beyond
			// MaxLevels may ever exist (the last valid level is terminal).
			ushort max = config.MaxLevels;
			if (level >= max) throw new ArgumentOutOfRangeException(nameof(level), level, $"Must be below maximum of {max}.");

			tower.OnLevelCreated(level);

			// 15-0030: solve.tower.level_count. Levels are created in strictly increasing
			// index order (see _levelCounts' field doc), so "level + 1" here is always this
			// tower's new high-water mark -- no comparison against the previous value needed.
			// The gauge source itself is registered only once per tower, at level 0, since
			// EngineInstruments keys one reader per owner.
			StrongBox<long> levelCountBox = _levelCounts.GetOrCreateValue(tower);
			levelCountBox.Value = level + 1;
			if (level == 0)
				EngineInstruments.RegisterLevelCountSource(tower, () => Volatile.Read(ref levelCountBox.Value));

			Index = level;
			PoolSize = config.PoolSize.GetPoolSize(level);
			Tower = tower;
			IsMax = level + 1 >= config.MaxLevels;

			// AgeProtectionWindow is opt-in and null by default: when unset, both fields
			// below stay null and RankEntries's age branch never runs, leaving selection
			// byte-for-byte identical to before this feature existed. When set, opt the
			// factory into age-marker tracking (a no-op if an earlier level at this same
			// tower already enabled it -- every Level here shares one Factory) so genomes
			// flowing through Registration start acquiring markers.
			_ageProtectionWindow = config.AgeProtectionWindow;
			if (_ageProtectionWindow.HasValue && tower.Factory is GenomeFactoryBase<TGenome> ageAwareFactory)
			{
				ageAwareFactory.AgeTrackingEnabled = true;
				_ageSource = ageAwareFactory;
			}

			_nextLevel = new(CreateNextLevel);

			int poolCount = tower.Problem.Pools.Count;
			BestLevelFitness = new double[poolCount][];
			BestProgressiveFitness = new double[poolCount][];

			// Reusable per-lens ranking buffers for RankEntries (see field doc comment below).
			_rankedPools = new LevelEntry<TGenome>[poolCount][];
			for (int p = 0; p < poolCount; p++)
				_rankedPools[p] = new LevelEntry<TGenome>[PoolSize];

			Pool = Channel.CreateBounded<LevelEntry<TGenome>>(new BoundedChannelOptions(PoolSize)
			{
				SingleReader = true,
				SingleWriter = false,
				// Synchronous continuations fuse reader/writer work onto one stack
				// across levels, degrading thread-pool fairness under load.
				AllowSynchronousContinuations = false
			});

			var buffer = new LevelEntry<TGenome>[PoolSize];
			// Passing the tower's token here does double duty: it lets this reader exit
			// once cancelled instead of waiting forever for another item (the channel is
			// never Complete()-d), and — since a promotion write inside ProcessReceived
			// (via ProcessSelection -> PromoteAsync -> Tower.DispatchAsync) also observes
			// this same token — an OperationCanceledException raised there unwinds out of
			// this receiver callback as a genuine cancellation. That makes the ValueTask
			// below settle into the Canceled state rather than Faulted, so the
			// OnlyOnFaulted continuation below correctly treats it as a normal shutdown
			// signal instead of a level fault.
			//
			// config.IdleFlushAfter is opt-in and null by default: RunPoolReaderAsync below
			// is that exact original fixed-fill-only loop, untouched, and is what still runs
			// whenever the option is unset. RunPoolReaderWithIdleFlushAsync is the opt-in
			// alternative, used only when a level owner explicitly configures an idle window.
			Task readerTask = config.IdleFlushAfter is { } idleFlushAfter
				? RunPoolReaderWithIdleFlushAsync(buffer, idleFlushAfter)
				: RunPoolReaderAsync(buffer);

			_ = readerTask.ContinueWith(
				t => Tower.OnLevelFault(Index, t.Exception!.GetBaseException()),
				CancellationToken.None,
				TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
				TaskScheduler.Default);
		}

		/// <summary>
		/// The scheme's original pool reader: processes selection only once
		/// <paramref name="buffer"/> fills to exactly <see cref="PoolSize"/>. Used whenever
		/// <see cref="ISchemeConfig.IdleFlushAfter"/> is unset (the default) and left
		/// byte-for-byte as it always was, so that default behavior is unaffected by the
		/// idle-flush feature.
		/// </summary>
		private Task RunPoolReaderAsync(LevelEntry<TGenome>[] buffer)
		{
			int index = 0;
			return Pool.Reader.ReadAllAsync(async e =>
			{
				buffer[index++] = e;
				if (index == PoolSize) index = await ProcessReceived(buffer).ConfigureAwait(false);
			}, Tower.CancellationToken)
			.AsTask();
		}

		/// <summary>
		/// Opt-in pool reader used when <see cref="ISchemeConfig.IdleFlushAfter"/> is set.
		/// Behaves exactly like <see cref="RunPoolReaderAsync"/> whenever the buffer fills to
		/// <see cref="PoolSize"/>, but additionally flushes a non-empty, partially-filled
		/// buffer through selection once no new entry has arrived for
		/// <paramref name="idleFlushAfter"/> — so a level parked one (or more) entries short of
		/// a full pool doesn't hold its in-flight genomes forever once upstream inflow pauses.
		/// A cohort of exactly one entry is never flushed (see <see cref="ProcessSelection"/>'s
		/// <c>midPoint</c> math — selection means competing against at least one other entry),
		/// so that case just keeps waiting.
		///
		/// Uses a read-timeout pattern — a linked <see cref="CancellationTokenSource"/> created
		/// fresh per pending read and cancelled after <paramref name="idleFlushAfter"/> —
		/// rather than a dedicated per-level timer: a level with nothing buffered pays for no
		/// timer at all (the very first read waits unconditionally), and an active level's
		/// window is naturally renewed each time a new read begins.
		/// </summary>
		private async Task RunPoolReaderWithIdleFlushAsync(LevelEntry<TGenome>[] buffer, TimeSpan idleFlushAfter)
		{
			ChannelReader<LevelEntry<TGenome>> reader = Pool.Reader;
			CancellationToken shutdown = Tower.CancellationToken;
			int index = 0;

			while (true)
			{
				LevelEntry<TGenome> entry;
				if (index == 0)
				{
					// Nothing buffered yet: no idle window to enforce, so wait unconditionally
					// for the first entry instead of paying for a timer with nothing to flush.
					entry = await reader.ReadAsync(shutdown).ConfigureAwait(false);
				}
				else
				{
					using CancellationTokenSource idleCts = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
					idleCts.CancelAfter(idleFlushAfter);
					try
					{
						entry = await reader.ReadAsync(idleCts.Token).ConfigureAwait(false);
					}
					catch (OperationCanceledException) when (!shutdown.IsCancellationRequested)
					{
						// Idle window elapsed with the buffer non-empty. Flush the partial
						// cohort through selection, unless it's a single entry with no
						// competitor yet — a cohort of one is never selected on.
						if (index >= 2)
							index = await ProcessReceived(buffer, index).ConfigureAwait(false);

						continue;
					}
				}

				buffer[index++] = entry;
				if (index == PoolSize) index = await ProcessReceived(buffer).ConfigureAwait(false);
			}
		}

		protected virtual ValueTask<int> ProcessReceived(LevelEntry<TGenome>[] fullBuffer)
			=> ProcessReceived(fullBuffer, PoolSize);

		/// <summary>
		/// Runs selection over the first <paramref name="count"/> entries of
		/// <paramref name="buffer"/> — <see cref="PoolSize"/> for a normally-filled buffer
		/// (see <see cref="ProcessReceived(LevelEntry{TGenome}[])"/>), or fewer for a partial
		/// cohort flushed early by <see cref="RunPoolReaderWithIdleFlushAsync"/>.
		/// </summary>
		protected virtual ValueTask<int> ProcessReceived(LevelEntry<TGenome>[] buffer, int count)
			=> ProcessSelection(buffer, count, RankEntries(buffer, count));

		protected static (bool success, bool isFresh) UpdateFitnessesIfBetter(
			in Span<double[]> registry,
			in ReadOnlySpan<double> contending,
			int fitnessIndex)
		{
			ref double[] fRef = ref registry[fitnessIndex];
			double[]? defending;
			double[]? contendingArray = null;
			while ((defending = fRef) is null || contending.IsGreaterThan(defending.AsSpan()))
			{
				contendingArray ??= contending.ToArray();
				if (Interlocked.CompareExchange(ref fRef, contendingArray, defending) == defending)
					return (true, defending is null);
			}

			return (false, false);
		}

		/// <summary>
		/// Reusable per-lens ranking buffers for <see cref="RankEntries"/> -- always allocated
		/// exactly <see cref="PoolSize"/> long, once in the constructor. Safe to reuse across
		/// selections because <see cref="Pool"/>'s reader is <c>SingleReader</c> and always
		/// awaits <see cref="ProcessSelection"/> to completion before reading (or flushing)
		/// again -- so selections for a given <see cref="Level"/> instance never overlap. Each
		/// <see cref="RankEntries"/> call fully overwrites indices <c>[0, count)</c> of every
		/// slot via <see cref="Array.Copy(Array, Array, int)"/> before sorting that same range,
		/// which is all <see cref="ProcessSelection"/> ever reads back -- so this remains safe
		/// even for a partial cohort (<c>count &lt; PoolSize</c>, from an idle-flush trigger in
		/// <see cref="RunPoolReaderWithIdleFlushAsync"/>): indices at or beyond <c>count</c> may
		/// still hold stale entries from a prior, larger selection, but nothing ever looks past
		/// <c>count</c> to observe them.
		/// </summary>
		private readonly LevelEntry<TGenome>[][] _rankedPools;

		protected LevelEntry<TGenome>[][] RankEntries(LevelEntry<TGenome>[] pool, int count)
		{
			int poolCount = Tower.Problem.Pools.Count;
			LevelEntry<TGenome>[][] result = _rankedPools;

			// Age-restricted ranking lens (25-0024, opt-in via SchemeConfig.AgeProtectionWindow):
			// classify "young" entries once per selection call -- not once per pool lens below,
			// since a genome's age doesn't depend on which fitness dimension is being ranked --
			// then let TryPromoteBestYoungLoser rescue each pool's best-ranked young loser after
			// its normal fitness sort. Left entirely out of the loop when _ageSource is null
			// (the default, disabled state): RankEntries then does exactly what it always did,
			// with only one extra null check paid here, so behavior stays byte-for-byte
			// unchanged on the disabled path.
			HashSet<LevelEntry<TGenome>>? young = null;
			if (_ageProtectionWindow is int window && _ageSource is { } ageSource)
			{
				young = _youngScratch ??= [];
				young.Clear();
				long currentAge = ageSource.CurrentAgeCounter;
				for (int i = 0; i < count; i++)
				{
					LevelEntry<TGenome> e = pool[i];
					long? marker = ageSource.GetAgeMarker(e.Progress.Genome);
					if (marker.HasValue && currentAge - marker.Value < window)
						young.Add(e);
				}
			}

			// Epsilon-lexicase ranking lens (25-0023, opt-in via SchemeConfig.RankingMode):
			// computed at most once per selection call -- unlike the aggregate comparer, which
			// is inherently per-pool (GetScoreComparer(i) ranks pool i's own Transform-derived
			// fitness), lexicase ranks purely from each entry's raw per-case results
			// (LevelEntry.CaseResults), which don't vary by pool -- so every pool lens below
			// shares the one ordering computed here instead of recomputing an equivalent one
			// per pool. Left null (and RankEntries then does exactly what it always did) unless
			// RankingMode is EpsilonLexicase AND every entry in this cohort actually carries
			// case results -- see IProblem.ProcessSampleCasesAsync's opt-out contract; a
			// problem that never overrides it (or a run using the default Aggregate mode) pays
			// zero extra cost here beyond the one enum comparison.
			LevelEntry<TGenome>[]? lexicaseOrder = null;
			if (Tower.Config.RankingMode == RankingMode.EpsilonLexicase
				&& HasCaseResultsThroughout(pool, count))
			{
				LevelEntry<TGenome>[] scratch = _lexicaseScratch ??= new LevelEntry<TGenome>[PoolSize];
				Array.Copy(pool, scratch, count);
				RankByEpsilonLexicase(scratch, count, Tower.Factory.RandomSource);
				lexicaseOrder = scratch;
			}

			for (int i = 0; i < poolCount; i++)
			{
				LevelEntry<TGenome>[] temp = result[i];
				if (lexicaseOrder is not null)
				{
					Array.Copy(lexicaseOrder, temp, count);
				}
				else
				{
					Array.Copy(pool, temp, count);
					IComparer<LevelEntry<TGenome>> comparer = LevelEntry<TGenome>.GetScoreComparer(i);
					// The comparer is a self-contained total order; any exception here is a
					// real defect and must propagate to the level fault path.
					Array.Sort(temp, 0, count, comparer);
				}

				if (young is { Count: > 0 })
					TryPromoteBestYoungLoser(temp, count, young);
			}

			return result;
		}

		/// <summary>
		/// True only when every entry in <paramref name="pool"/>[0, <paramref name="count"/>)
		/// carries non-null <see cref="LevelEntry{TGenome}.CaseResults"/> -- the precondition
		/// <see cref="RankByEpsilonLexicase"/> assumes. A mixed cohort (some entries with case
		/// results, some without) shouldn't occur in practice -- whether a problem supplies
		/// them is fixed for its whole lifetime, see <see cref="IProblem{TGenome}.ProcessSampleCasesAsync"/>'s
		/// contract -- but checking defensively here means an inconsistent cohort falls back to
		/// the ordinary aggregate comparer instead of <see cref="RankByEpsilonLexicase"/> ever
		/// having to handle a null <see cref="LevelEntry{TGenome}.CaseResults"/> mid-algorithm.
		/// </summary>
		private static bool HasCaseResultsThroughout(LevelEntry<TGenome>[] pool, int count)
		{
			for (int i = 0; i < count; i++)
			{
				if (pool[i].CaseResults is null) return false;
			}

			return true;
		}

		protected void ProcessChampion(int poolIndex, LevelProgress<TGenome> champ)
		{
			Debug.Assert(poolIndex >= 0);
			if (Index == 0) return; // Ignore champions from first level.
			Tower.Problem.Pools[poolIndex].Champions?.Add(champ.Genome, champ.Fitnesses[poolIndex]);
			// 15-0030: solve.champion.gene_count, recorded at the champion broadcast point.
			EngineInstruments.RecordChampionGeneCount(champ.Genome.GeneCount);
			Tower.Broadcast(champ, poolIndex);
		}

		/// <summary>
		/// Submits <paramref name="contender"/> for this level's evaluation on the tower's
		/// shared bounded worker stage and returns once accepted onto that queue (subject to
		/// backpressure when it's full) — NOT once evaluation has completed. This is what
		/// lets the producer loop keep calling <see cref="ProblemTower.PostAsync"/> for the
		/// next genome, and a level's pool reader keep draining <see cref="Pool"/>, while
		/// prior genomes evaluate concurrently across the worker stage instead of inline on
		/// the caller's own await chain.
		/// </summary>
		public virtual ValueTask PostAsync(LevelProgress<TGenome> contender)
			=> Tower.DispatchAsync(this, contender);

		/// <summary>
		/// Evaluates <paramref name="contender"/> for this level, merges the result into its
		/// <see cref="LevelProgress{TGenome}"/>, and either pools the entry or fast-tracks it
		/// into the next level — the selection semantics this scheme has always had: a
		/// contender's fitness for this level is always fully merged before it enters
		/// <see cref="Pool"/> or is handed to <see cref="NextLevel"/>. Runs on one of the
		/// tower's shared evaluation workers rather than on whichever chain submitted it.
		/// Any failure is routed to <see cref="ProblemTower.OnLevelFault"/> instead of being
		/// allowed to escape, mirroring the fault-surfacing contract this level's own pool
		/// reader already upholds for selection-time failures (see the constructor's
		/// <c>ReadAllAsync</c> <c>ContinueWith</c>) — so a fault occurring anywhere along a
		/// genome's fast-track chain is attributed to the level it actually happened at.
		/// Marked <see langword="internal"/> (in addition to <see langword="protected"/>) so
		/// the tower's shared worker loop (<see cref="ProblemTower"/>, a sibling type) can
		/// invoke it directly for a dequeued work item without an intermediate delegate.
		/// </summary>
		protected internal virtual async ValueTask ProcessContenderSafelyAsync(LevelProgress<TGenome> contender)
		{
			try
			{
				// Manual loop instead of Select(...).ToArray(): avoids the LINQ iterator and
				// the per-call instance-capturing delegate that Select would allocate here,
				// while still producing exactly one array sized to the known pool count (every
				// IProblem.ProcessSampleAsync implementation yields exactly Pools.Count
				// Fitness values -- see ProblemBase.ProcessSampleAsync -- the same invariant
				// this method's UpdateFitnessesIfBetter(..., i) calls already rely on).
				int poolCount = Tower.Problem.Pools.Count;
				var result = new (ImmutableArray<double> levelFitness, bool success, bool fresh)[poolCount];
				int idx = 0;
				foreach (Fitness fitness in await Tower.Problem.ProcessSampleAsync(contender.Genome, Index).ConfigureAwait(false))
				{
					ImmutableArray<double> levelFitness = fitness.Results.Sum;
					(bool levelWinner, bool isFirstofLevel) = UpdateFitnessesIfBetter(BestLevelFitness.Span, levelFitness.AsSpan(), idx);

					Fitness fitnessRecord = contender.Fitnesses[idx];
					ReadOnlySpan<double> fitnessRecordNew = fitnessRecord.Merge(levelFitness).Average.AsSpan();
					(bool progressiveWinner, bool isFirstofProgressive) = UpdateFitnessesIfBetter(BestProgressiveFitness.Span, fitnessRecordNew, idx);

					Debug.Assert(fitnessRecord.MetricAverages.All(ma => ma.Value <= ma.Metric.MaxValue));

					result[idx] = (
						levelFitness,
						success: levelWinner || progressiveWinner,
						fresh: isFirstofLevel || isFirstofProgressive
					);
					idx++;
				}

				bool won = result.Any(r => r.success);

				if (IsTop)
				{
					for (byte i = 0; i < result.Length; i++)
					{
						if (result[i].success) // Get the champion for each fitness.
							ProcessChampion(i, contender);
					}
				}

				// Single authoritative inflow point to the champion queue for this
				// evaluation event (EnqueueChampion's own variation/breeding/mutation
				// fan-out is untouched below — this only decides *whether* and *how
				// often* it gets called). This used to be two independent paths that
				// could both fire for genomes originating from the very same climb:
				// (1) this method's own fast-track branch below called EnqueueChampion
				// directly whenever a non-top, non-max level produced a win, and
				// (2) TowerSchemeBase's constructor subscribed to every tower Broadcast
				// and called EnqueueChampion again (`this.Subscribe(e =>
				// Factory[0].EnqueueChampion(e.Genome), ...)`), where Broadcast is
				// raised by ProcessChampion just above — but only when IsTop, and once
				// per winning pool index. A single genome's ascent visits many levels;
				// whenever it won on an already-topped level it hit path (1), and
				// whenever it happened to win while a level was still the frontier
				// (including every win at the terminal/max level, where IsTop never
				// becomes false) it hit path (2) as well — redundant inflow that
				// inflated breeding-stock churn without changing which genomes counted
				// as champions. Gating a single call on `won` — computed once above
				// from the same per-pool-index success flags ProcessChampion already
				// consumes, and evaluated regardless of IsTop/IsMax — covers both of
				// those original sources uniformly: fast-track wins on already-topped
				// levels, and top-level/frontier wins (previously only reachable via
				// the now-removed TowerSchemeBase subscription). ProcessChampion /
				// Broadcast above keeps its own, separate job — recording the official
				// per-pool Champions registry and notifying external observers
				// (dashboard, StagnationMonitor, tests) — decoupled from feeding the
				// breeding queue.
				if (won)
					Tower.Factory[0].EnqueueChampion(contender.Genome);

				if (IsMax || IsTop // Let the pools fill up first before fast-tracking new champions.
								   //|| result.Any(r => r.fresh)
					|| !won)
				{
					// Manual copy instead of Select(...).ToArray(): same allocation-avoidance
					// rationale as the result array above -- one array of exactly the needed
					// size, no LINQ iterator/delegate.
					var scores = new ImmutableArray<double>[result.Length];
					for (int i = 0; i < scores.Length; i++)
						scores[i] = result[i].levelFitness;

					// 25-0023: per-case results are only ever fetched for an entry that's
					// actually about to be pooled (this branch) -- not for a fast-tracked
					// genome continuing straight to NextLevel below, which never becomes a
					// LevelEntry and so has no use for them -- and only when RankingMode opts
					// into EpsilonLexicase. On the default Aggregate path this check short-
					// circuits and ProcessSampleCasesAsync is never called, so no extra
					// evaluation work is scheduled and behavior stays byte-for-byte identical
					// to before this feature existed.
					IReadOnlyList<CaseResult>? caseResults = Tower.Config.RankingMode == RankingMode.EpsilonLexicase
						? await Tower.Problem.ProcessSampleCasesAsync(contender.Genome, Index).ConfigureAwait(false)
						: null;

					LevelEntry<TGenome> entry = LevelEntry<TGenome>.Init(in contender, scores, contender.Losses[Index], won, caseResults);

					// Prefer the non-blocking path: Pool has its own dedicated reader (this
					// level's ReadAllAsync loop above) that is normally keeping capacity free.
					// If it's momentarily saturated, hand the write off instead of blocking
					// this shared evaluation worker on it — the worker must stay free to keep
					// draining the tower's shared dispatch queue, since this level's own
					// reader may itself be mid-promotion, awaiting capacity on that very
					// queue. Blocking here could otherwise create a cross-channel circular
					// wait under sustained load.
					if (!Pool.Writer.TryWrite(entry))
					{
						_ = Task.Run(async () =>
						{
							try
							{
								// Honor the tower's cancellation token so a cancel issued while
								// this fallback write is parked on a full Pool unblocks it
								// immediately instead of waiting indefinitely for capacity the
								// congestion itself is withholding.
								await Pool.Writer.WriteAsync(entry, Tower.CancellationToken).ConfigureAwait(false);
							}
							catch (OperationCanceledException)
							{
								// Normal shutdown: cancelled while parked on a full Pool. Not a
								// level fault.
							}
							catch (Exception ex)
							{
								Tower.OnLevelFault(Index, ex);
							}
						});
					}
				}
				else
				{
					// Champion inflow for this win was already handled uniformly above
					// (see the `won` block) — this branch now only continues the climb.
					// Hand this genome's next level off to the shared queue so any free
					// worker (not necessarily this one) can pick it up — champions on
					// newly-created levels fast-track often (a brand new level's first
					// arrival always "wins" it), so a long climb can otherwise chain many
					// levels sequentially on one worker while the rest of the pool sits
					// idle. TryDispatch is non-blocking: on success a different worker
					// continues this chain; when the queue is momentarily full we fall back
					// to continuing in-process rather than blocking this worker on a write
					// to the very queue only workers drain (blocking there could
					// self-deadlock under load).
					await ContinueAsync(NextLevel, contender).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException)
			{
				// Normal shutdown: cancellation observed while evaluating, merging, writing
				// to Pool, or dispatching to the next level (Tower.DispatchAsync and the
				// Pool fallback write above both honor Tower.CancellationToken). Not a level
				// fault — swallowed here rather than reported via OnLevelFault.
			}
			catch (Exception ex)
			{
				Tower.OnLevelFault(Index, ex);
			}
		}

		private ValueTask ContinueAsync(IAsyncLevel<TGenome> next, LevelProgress<TGenome> contender)
		{
			if (next is not Level level)
				return next.PostAsync(contender);

			return Tower.TryDispatch(level, contender)
				? default
				: level.ProcessContenderSafelyAsync(contender);
		}

		protected ValueTask PromoteAsync(LevelEntry<TGenome> champion)
		{
			Debug.Assert(!IsMax, "Terminal levels must not promote.");
			LevelProgress<TGenome> progress = champion.Progress;
			LevelEntry<TGenome>.Pool.Give(champion);
			return NextLevel.PostAsync(progress);
		}

		/// <summary>
		/// Runs selection over the first <paramref name="count"/> entries ranked into
		/// <paramref name="pools"/> (see <see cref="RankEntries"/>). <paramref name="count"/>
		/// is <see cref="PoolSize"/> for a normally-filled buffer and the live entry count for
		/// a partial cohort flushed early by <see cref="RunPoolReaderWithIdleFlushAsync"/> --
		/// callers must never invoke this with <paramref name="count"/> below 2 (a cohort of
		/// one has no competitor to select against; see that method's guard).
		/// </summary>
		protected async ValueTask<int> ProcessSelection(LevelEntry<TGenome>[] buffer, int count, LevelEntry<TGenome>[][] pools)
		{
			int poolCount = Tower.Problem.Pools.Count;
			Debug.Assert(poolCount != 0);
			Debug.Assert(count >= 2, "ProcessSelection requires a cohort of at least 2 (see RunPoolReaderWithIdleFlushAsync's minimum-cohort guard).");
			int midPoint = count / 2;
			SharedPool<List<LevelEntry<TGenome>>> lPool = ListPool<LevelEntry<TGenome>>.Shared;
			SharedPool<HashSet<string>> hsPool = HashSetPool<string>.Shared;
			HashSet<string> processed = hsPool.Take();
			List<LevelEntry<TGenome>> toPromote = lPool.Take();
			List<LevelEntry<TGenome>> toKill = lPool.Take();

			//var isTop = IsTop;
			// Remaining top 50% (winners) should go before any losers.
			for (int i = 0; i < midPoint; ++i)
			{
				for (int p = 0; p < poolCount; ++p)
				{
					LevelEntry<TGenome> e = pools[p][i];
					LevelProgress<TGenome> progress = e.Progress;
					string hash = progress.Genome.Hash;
					if (processed.Add(hash)) toPromote.Add(e);
				}
			}

			// Distribute losers either back into the pool, pass them to the next level, or let them disapear (rejected).
			SchemeConfig.Values config = Tower.Config;
			ushort maxLoses = config.MaxLevelLoss;
			ushort maxRejection = config.MaxConsecutiveRejections;
			ushort percentRejectionLimit = config.PercentRejectedBeforeElimination;
			int retained = 0;
			for (int i = midPoint; i < count; ++i)
			{
				for (int p = 0; p < poolCount; ++p)
				{
					LevelEntry<TGenome> loser = pools[p][i];
					LevelProgress<TGenome> progress = loser.Progress;
					string hash = progress.Genome.Hash;
					if (!processed.Add(hash)) continue;

					int lossCount = loser.LossCount.Increment();
					if (lossCount > maxLoses)
					{
						LossTracker lossRecord = progress.Losses;
						int totalRejections = lossRecord.IncrementRejection(Index);

						if (lossRecord.ConsecutiveRejection > maxRejection
							&& 100 * totalRejections > Index * percentRejectionLimit)
						{
							// Permanently rejected.
							toKill.Add(loser);
						}
						else
						{
							// Survived for another try.
							toPromote.Add(loser);
						}
					}
					else
					{
						Debug.Assert(loser.LossCount == lossCount, $"LossCount: {loser.LossCount}, expected {lossCount}.");
						buffer[retained++] = loser;
					}
				}
			}

			hsPool.Give(processed);
			Array.Clear(buffer, retained, buffer.Length - retained);

#if DEBUG
			Debug.Assert(toPromote.Distinct().Count() == toPromote.Count);
#endif
			if (IsMax)
			{
				// Terminal level: there is no next level to promote into. Feed the
				// pool-cohort's top-half survivors back into breeding as this level's
				// substitute for promotion, and release the entries — promoting here
				// would construct a level past the cap and dam the tower.
				//
				// Entries flagged Won already triggered an evaluation-time champion
				// enqueue (they set a new best-fitness record — see
				// ProcessContenderSafelyAsync's `won` branch, which also handles
				// recording into the Champions registry/Broadcast via IsTop). Skipping
				// those here avoids double-enqueueing the same win; cohort survivors
				// that ranked top-half WITHOUT individually winning are a distinct,
				// legitimate breeding signal this level would otherwise never provide
				// for them (there being no NextLevel to carry them forward) — those
				// still get their one and only enqueue below.
				foreach (LevelEntry<TGenome> p in toPromote)
				{
					if (!p.Won)
						Tower.Factory[0].EnqueueChampion(p.Progress.Genome);
					p.Progress.Dispose();
					LevelEntry<TGenome>.Pool.Give(p);
				}
			}
			else
			{
				foreach (LevelEntry<TGenome> p in toPromote) await PromoteAsync(p).ConfigureAwait(false);
			}

			lPool.Give(toPromote);

			foreach (LevelEntry<TGenome> k in toKill)
			{
				k.Progress.Dispose();
				LevelEntry<TGenome>.Pool.Give(k);
			}

			return retained;
		}
	}
}
