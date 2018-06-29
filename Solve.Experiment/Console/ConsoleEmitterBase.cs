using Open.Collections;
using Open.Threading;
using System;
using System.Linq;

namespace Solve.Experiment.Console
{
	public class ConsoleEmitterBase<TGenome>
		where TGenome : IGenome
	{
		public readonly AsyncFileWriter LogFile;
		public readonly uint SampleMinimum;

		public ConsoleEmitterBase(uint sampleMinimum = 50, string logFilePath = null)
		{
			LogFile = logFilePath == null ? null : new AsyncFileWriter(logFilePath, 1000);
			SampleMinimum = sampleMinimum;
		}

		public FitnessScore[] LastScore;
		public string LastHash;
		public CursorRange LastTopGenomeUpdate;

		public bool EmitTopGenomeStats(TGenome genome, (IProblem<TGenome> Problem, IFitness Fitness)[] stats)
		{
			var sc = stats.Max(s => s.Fitness.SampleCount);
			if (sc < SampleMinimum) return false;

			var asReduced = genome is IReducibleGenome<TGenome> r ? r.AsReduced() : genome;
			var scores = stats.Select(s => s.Fitness.SnapShot()).ToArray();
			return ThreadSafety.LockConditional(
				SynchronizedConsole.Sync,
				() => sc >= SampleMinimum && (LastScore == null || LastScore.Where((ls, i) =>
				{
					var f = scores[i];
					return ls < f && ls.SampleCount < f.SampleCount;
				}).Any()),
				() => SynchronizedConsole.OverwriteIfSame(ref LastTopGenomeUpdate, () => LastHash == genome.Hash,
					cursor =>
					{
						if (asReduced.Equals(genome))
							System.Console.WriteLine("{0}", genome.Hash);
						else
							System.Console.WriteLine("{0}\n=>{1}", genome.Hash, asReduced.Hash);

						LastScore = scores;
						LastHash = genome.Hash;

						foreach (var (Problem, Fitness) in stats)
						{
							EmitFitnessScoreWithLabels(Problem, Fitness);
							System.Console.WriteLine();
							OnEmittingGenome(Problem, genome, Fitness);
						}
					}));
		}

		protected virtual void OnEmittingGenome(IProblem<TGenome> p, TGenome genome, IFitness fitness)
		{
			LogFile?.AddLine($"{DateTime.Now},{p.ID},{p.TestCount},{string.Join(',', fitness.Scores.ToStringArray())},");
		}

		public static void EmitFitnessScoreWithLabels(IProblem<TGenome> problem, IFitness fitness)
		{
			var labels = problem.FitnessLabels;
			var scoreStrings = fitness.Scores.Select((s, i) => String.Format(labels[i], s));

			System.Console.WriteLine("{0}:\t{1} ({2:n0} samples)", problem.ID, scoreStrings.JoinToString(", "), fitness.SampleCount);
		}

	}
}
