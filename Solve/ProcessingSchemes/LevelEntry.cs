using Open.Disposable;
using Open.Memory;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Solve.ProcessingSchemes
{
	public class LevelEntry<TGenome> : IRecyclable
	{
		public (TGenome Genome, Fitness[] Fitness) GenomeFitness { get; private set; }

		public ImmutableArray<double>[] Scores { get; private set; } = default!;
		public ushort LevelLossRecord { get; private set; }

		private static readonly ConcurrentDictionary<int, IComparer<LevelEntry<TGenome>>> Comparers
			= new ConcurrentDictionary<int, IComparer<LevelEntry<TGenome>>>();
		public static IComparer<LevelEntry<TGenome>> GetScoreComparer(int index)
			=> Comparers.GetOrAdd(index, i => new LevelEntryScoreComparer(i));

		class LevelEntryScoreComparer : IComparer<LevelEntry<TGenome>>
		{
			public readonly int ScoreIndex;
			public LevelEntryScoreComparer(int scoreIndex)
			{
				ScoreIndex = scoreIndex;
			}
			public int Compare(LevelEntry<TGenome> x, LevelEntry<TGenome> y)
				=> CollectionComparer.Double.Descending.Compare(x.Scores[ScoreIndex], y.Scores[ScoreIndex]);
		}

		public static LevelEntry<TGenome> Merge(in (TGenome Genome, Fitness[] Fitness) gf, IReadOnlyList<Fitness> scores)
		{
			var progressive = gf.Fitness;
			var len = scores.Count;
			var dScore = new ImmutableArray<double>[scores.Count];
			Debug.Assert(len == progressive.Length);

			for (var i = 0; i < len; i++)
			{
				var score = scores[i].Results.Sum;
				dScore[i] = score;
				progressive[i].Merge(score);
			}

			return Init(in gf, dScore);
		}

		public void Recycle()
		{
			GenomeFitness = default;
			Scores = null!;
			LevelLossRecord = 0;
		}

		public static LevelEntry<TGenome> Init(
			in (TGenome Genome, Fitness[] Fitness) gf,
			ImmutableArray<double>[] scores,
			ushort losses = 0)
		{
			var e = Pool.Take();
			e.GenomeFitness = gf;
			e.Scores = scores;
			e.LevelLossRecord = losses;
			return e;
		}

		public static LevelEntry<TGenome> Init(
			in (TGenome Genome, Fitness[] Fitness) gf,
			IEnumerable<ImmutableArray<double>> scores,
			ushort losses = 0)
			=> Init(in gf, scores as ImmutableArray<double>[] ?? scores.ToArray(), losses);

		public int IncrementLoss()
			=> LevelLossRecord++;

		public static readonly InterlockedArrayObjectPool<LevelEntry<TGenome>> Pool
			= InterlockedArrayObjectPool.Create<LevelEntry<TGenome>>();
	}

}
