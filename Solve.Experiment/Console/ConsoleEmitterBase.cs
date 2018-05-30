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

		public FitnessScore? LastScore;
		public string LastHash;
		public CursorRange LastTopGenomeUpdate;

		public void EmitTopGenomeStats((IProblem<TGenome> Problem, TGenome Genome) kvp)
			=> EmitTopGenomeStatsInternal(kvp.Problem, kvp.Genome);

		public void EmitTopGenomeStats((IProblem<TGenome>, IGenomeFitness<TGenome>) kvp)
			=> EmitTopGenomeStatsInternal(kvp.Item1, kvp.Item2);

		public void EmitTopGenomeStats(IProblem<TGenome> p, IGenomeFitness<TGenome> gf)
			=> EmitTopGenomeStatsInternal(p, gf);

		public void EmitTopGenomeStats(IProblem<TGenome> p, TGenome genome)
			=> EmitTopGenomeStatsInternal(p, genome);

		protected bool EmitTopGenomeStatsInternal(IProblem<TGenome> p, IGenomeFitness<TGenome> gf)
			=> EmitTopGenomeStatsInternal(p, gf.Genome, gf.Fitness);

		protected bool EmitTopGenomeStatsInternal(IProblem<TGenome> p, TGenome genome, IFitness fitness = null)
		{
			var sc = fitness.SampleCount;
			if (sc < SampleMinimum) return false;

			var f = (fitness ?? p.GetFitnessFor(genome).Value.Fitness).SnapShot();

			var asReduced = genome is IReducibleGenome<TGenome> r ? r.AsReduced() : genome;
			return ThreadSafety.LockConditional(
				SynchronizedConsole.Sync,
				() => sc >= SampleMinimum && (!LastScore.HasValue || LastScore.Value < f || LastHash == genome.Hash),
				() => SynchronizedConsole.OverwriteIfSame(ref LastTopGenomeUpdate, () => LastHash == genome.Hash,
					cursor =>
					{
						if (asReduced.Equals(genome))
							System.Console.WriteLine("{0}:\t{1}", p.ID, genome.Hash);
						else
							System.Console.WriteLine("{0}:\t{1}\n=>\t{2}", p.ID, genome.Hash, asReduced.Hash);

						EmitFitnessScoreWithLabels(p, f);
						System.Console.WriteLine();

						LastScore = f;
						LastHash = genome.Hash;

						OnEmittingGenome(p, genome, f);

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

			System.Console.WriteLine("  \t{0} ({1:n0} samples)", scoreStrings.JoinToString(", "), fitness.SampleCount);
		}

	}
}
