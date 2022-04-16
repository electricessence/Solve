using Open.Disposable;
using Open.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Solve.ProcessingSchemes;

public class LevelEntry<TGenome> : IRecyclable
{
	public LevelProgress<TGenome> Progress { get; private set; } = null!;

	public ImmutableArray<double>[] Scores { get; private set; } = default!;

	InterlockedInt? _losses;
	public InterlockedInt LossCount => _losses ?? throw new InvalidOperationException("Accessing an uninitialized LevelEntry.");

	private static readonly ConcurrentDictionary<int, IComparer<LevelEntry<TGenome>>> Comparers = new();
	public static IComparer<LevelEntry<TGenome>> GetScoreComparer(int index)
		=> Comparers.GetOrAdd(index, i => new LevelEntryScoreComparer(i));

	class LevelEntryScoreComparer : IComparer<LevelEntry<TGenome>>
	{
		public readonly int ScoreIndex;
		public LevelEntryScoreComparer(int scoreIndex) => ScoreIndex = scoreIndex;
#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
		public int Compare(LevelEntry<TGenome> x, LevelEntry<TGenome> y)
			=> CollectionComparer.Double.Descending.Compare(x.Scores[ScoreIndex], y.Scores[ScoreIndex]);
#pragma warning restore CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
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
		var e = Pool.Take();
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

