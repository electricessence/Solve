using Open.Disposable;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Solve.ProcessingSchemes;

public class LevelEntry<TGenome> : IRecyclable
{
	public LevelProgress<TGenome> Progress { get; private set; } = null!;

	public IReadOnlyList<ImmutableArray<double>> Scores { get; private set; } = default!;

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
	}

	public static LevelEntry<TGenome> Init(
		in LevelProgress<TGenome> progress,
		in ImmutableArray<double>[] scores,
		InterlockedInt losses)
	{
		LevelEntry<TGenome> e = Pool.Take();
#if DEBUG
		Debug.Assert(e._losses is null);
#endif
		e.Progress = progress;
		e.Scores = scores;
		e._losses = losses;
		return e;
	}

	public static LevelEntry<TGenome> Init(
		in LevelProgress<TGenome> progress,
		IEnumerable<ImmutableArray<double>> scores,
		InterlockedInt losses)
		=> scores is ImmutableArray<double>[] s
			? Init(in progress, in s, losses)
			: Init(in progress, scores.ToArray(), losses);

	public static readonly InterlockedArrayObjectPool<LevelEntry<TGenome>> Pool
		= InterlockedArrayObjectPool.CreateAutoRecycle<LevelEntry<TGenome>>();
}
