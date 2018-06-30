using Open.Numeric;
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

		public ProcedureResults[] LastScore;
		public string LastHash;
		public CursorRange LastTopGenomeUpdate;

		public bool EmitTopGenomeStats(TGenome genome, (IProblem<TGenome> Problem, FitnessContainer Fitness)[] stats)
		{
			var sc = stats.Max(s => s.Fitness.SampleCount);
			if (sc < SampleMinimum) return false;

			var asReduced = genome is IReducibleGenome<TGenome> r ? r.AsReduced() : genome;
			var scores = stats.Select(s => s.Fitness.Results).ToArray();
			return ThreadSafety.LockConditional(
				SynchronizedConsole.Sync,
				() => sc >= SampleMinimum && (LastScore == null || LastScore.Where((ls, i) =>
				{
					var f = scores[i];
					return ls.Average.IsLessThan(f.Average) && ls.Count < f.Count;
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
							var results = Fitness.Results;
							EmitFitnessScoreWithLabels(Problem, results);
							System.Console.WriteLine();
							OnEmittingGenome(Problem, genome, results);
						}
					}));
		}

		protected virtual void OnEmittingGenome(IProblem<TGenome> p, TGenome genome, ProcedureResults fitness)
		{
			LogFile?.AddLine($"{DateTime.Now},{p.ID},{p.TestCount},{fitness.Average.Span.ToStringBuilder(',')},");
		}

		public static void EmitFitnessScoreWithLabels(IProblem<TGenome> problem, ProcedureResults fitness)
		{
			var labels = problem.FitnessLabels;
			var scoreStrings = fitness.Average.ToArray().Select((s, i) => String.Format(labels[i], s)).ToArray();

			System.Console.WriteLine("{0}:\t{1} ({2:n0} samples)", problem.ID, String.Join(", ", scoreStrings), fitness.Count);
		}

	}
}
