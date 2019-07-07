using System.Collections.Generic;
using System.Diagnostics;
using Open.Disposable;

namespace Solve.ProcessingSchemes
{
	/// <summary>
	/// An object-pooled class for use with retaining genome fitness within a level.
	/// </summary>
	/// <typeparam name="TGenome">The type of genome held.</typeparam>
	public class LevelEntry<TGenome> : IRecyclable
		where TGenome : class, IGenome
	{
		public (TGenome Genome, Fitness[] Fitness) GenomeFitness { get; private set; }
		public double[][] Scores { get; private set; }
		public ushort LevelLossRecord { get; private set; }

		public static LevelEntry<TGenome> Merge(in (TGenome Genome, Fitness[] Fitness) gf, IReadOnlyList<Fitness> scores)
		{
			var progressive = gf.Fitness;
			var len = scores.Count;
			var dScore = new double[scores.Count][];
			Debug.Assert(len == progressive.Length);

			for (var i = 0; i < len; i++)
			{
				var score = scores[i].Results.Sum.ToArray();
				dScore[i] = score;
				progressive[i].Merge(score);
			}

			return Init(in gf, dScore);
		}

		public void Recycle()
		{
			GenomeFitness = default;
			Scores = null;
			LevelLossRecord = 0;
		}

		public static LevelEntry<TGenome> Init(
			in (TGenome Genome, Fitness[] Fitness) gf,
			double[][] scores,
			ushort losses = 0)
		{
			var e = Pool.Take();
			e.GenomeFitness = gf;
			e.Scores = scores;
			e.LevelLossRecord = losses;
			return e;
		}

		public int IncrementLoss()
			=> LevelLossRecord++;

		public static readonly InterlockedArrayObjectPool<LevelEntry<TGenome>> Pool
			= InterlockedArrayObjectPool.Create<LevelEntry<TGenome>>();
	}

}
