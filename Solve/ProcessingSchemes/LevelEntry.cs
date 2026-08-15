using Open.Disposable;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Solve.ProcessingSchemes;

public class LevelEntry<TGenome> : IRecyclable
{
	public LevelProgress<TGenome> Progress { get; private set; } = null!;

	public IReadOnlyList<ImmutableArray<double>> Scores { get; private set; } = default!;

	/// <summary>
	/// Per-case results for this entry's evaluation (task 25-0023), or <see langword="null"/>
	/// when they weren't captured -- either <see cref="SchemeConfig.RankingMode"/> wasn't
	/// <see cref="SchemeConfig.RankingMode.EpsilonLexicase"/> for this evaluation (the default;
	/// see <c>TowerScheme{TGenome}.Level.ProcessContenderSafelyAsync</c>, which only calls
	/// <see cref="IProblem{TGenome}.ProcessSampleCasesAsync"/> when it was), or the problem
	/// doesn't override <see cref="IProblem{TGenome}.ProcessSampleCasesAsync"/> and so opted out
	/// of lexicase ranking entirely. Consumed by
	/// <c>TowerScheme{TGenome}.RankByEpsilonLexicase</c>; every entry in a single ranking call
	/// is expected to carry the same case count when non-null (all entries at a given level
	/// share one problem and one sampleId shape).
	/// </summary>
	public IReadOnlyList<CaseResult>? CaseResults { get; private set; }

	/// <summary>
	/// True when this entry's genome already triggered an evaluation-time champion-queue
	/// enqueue for this level (it set a new best-fitness record for at least one pool —
	/// see <c>ProcessContenderSafelyAsync</c>'s <c>won</c> flag). Terminal levels enqueue
	/// pool-cohort survivors again during <c>ProcessSelection</c> as their substitute for
	/// promotion (there is no next level); consulting this flag there lets that pass skip
	/// re-enqueueing genomes already credited, without skipping cohort survivors that
	/// ranked top-half without individually winning — those still need their one and only
	/// enqueue.
	/// </summary>
	public bool Won { get; private set; }

	private InterlockedInt? _losses;
	public InterlockedInt LossCount => _losses ?? throw new InvalidOperationException("Accessing an uninitialized LevelEntry.");

	private static readonly ConcurrentDictionary<int, IComparer<LevelEntry<TGenome>>> Comparers = new();
	public static IComparer<LevelEntry<TGenome>> GetScoreComparer(int index)
		=> Comparers.GetOrAdd(index, i => new LevelEntryScoreComparer(i));

	private sealed class LevelEntryScoreComparer(int scoreIndex) : IComparer<LevelEntry<TGenome>>
	{
		public readonly int ScoreIndex = scoreIndex;

		// Element-wise descending with double.CompareTo semantics: a self-contained
		// total order (NaN ranks below all values, so NaN-bearing scores sort last)
		// regardless of external comparer implementations.
		public int Compare(LevelEntry<TGenome>? x, LevelEntry<TGenome>? y)
		{
			if (ReferenceEquals(x, y)) return 0;
			if (x is null) return 1;
			if (y is null) return -1;

			ImmutableArray<double> a = x.Scores[ScoreIndex];
			ImmutableArray<double> b = y.Scores[ScoreIndex];
			Debug.Assert(a.Length == b.Length);
			int len = Math.Min(a.Length, b.Length);
			for (int i = 0; i < len; i++)
			{
				int c = b[i].CompareTo(a[i]);
				if (c != 0) return c;
			}

			return b.Length.CompareTo(a.Length);
		}
	}

	public void Recycle()
	{
		Progress = null!;
		Scores = null!;
		_losses = null;
		Won = false;
		CaseResults = null;
	}

	public static LevelEntry<TGenome> Init(
		in LevelProgress<TGenome> progress,
		in ImmutableArray<double>[] scores,
		InterlockedInt losses,
		bool won = false,
		IReadOnlyList<CaseResult>? caseResults = null)
	{
		LevelEntry<TGenome> e = Pool.Take();
#if DEBUG
		Debug.Assert(e._losses is null);
#endif
		e.Progress = progress;
		e.Scores = scores;
		e._losses = losses;
		e.Won = won;
		e.CaseResults = caseResults;
		return e;
	}

	public static LevelEntry<TGenome> Init(
		in LevelProgress<TGenome> progress,
		IEnumerable<ImmutableArray<double>> scores,
		InterlockedInt losses,
		bool won = false,
		IReadOnlyList<CaseResult>? caseResults = null)
		=> scores is ImmutableArray<double>[] s
			? Init(in progress, in s, losses, won, caseResults)
			: Init(in progress, scores.ToArray(), losses, won, caseResults);

	public static readonly InterlockedArrayObjectPool<LevelEntry<TGenome>> Pool
		= InterlockedArrayObjectPool.CreateAutoRecycle<LevelEntry<TGenome>>();
}
