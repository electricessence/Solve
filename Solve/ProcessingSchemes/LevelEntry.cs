using System.Collections.Generic;
using System.Diagnostics;

namespace Solve.ProcessingSchemes
{
	public class LevelEntry<TGenome>
		where TGenome : class, IGenome
	{
		public LevelEntry(in (TGenome Genome, Fitness[] Fitness) gf, double[][] scores)
		{
			GenomeFitness = gf;
			Scores = scores;
			LevelLossRecord = 0;
		}

		public readonly (TGenome Genome, Fitness[] Fitness) GenomeFitness;
		public readonly double[][] Scores;

		public ushort LevelLossRecord;
	}

	public static class LevelEntry
	{
		public static LevelEntry<TGenome> Merge<TGenome>(in (TGenome Genome, Fitness[] Fitness) gf, IReadOnlyList<Fitness> scores)
			where TGenome : class, IGenome
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

			return new LevelEntry<TGenome>(in gf, dScore);
		}
	}
}
