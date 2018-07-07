using Open.Memory;
using Open.Threading;
using System;
using System.Linq;
using System.Text;

namespace Solve.Experiment.Console
{
	public class ConsoleEmitterBase<TGenome>
		where TGenome : class, IGenome
	{
		public readonly AsyncFileWriter LogFile;
		public readonly uint SampleMinimum;

		public ConsoleEmitterBase(uint sampleMinimum = 50, string logFilePath = null)
		{
			LogFile = logFilePath == null ? null : new AsyncFileWriter(logFilePath, 1000);
			SampleMinimum = sampleMinimum;
		}

		public string LastHash;
		public CursorRange LastTopGenomeUpdate;

		public bool EmitTopGenomeStats(IProblem<TGenome> problem, TGenome genome, Fitness[] fitness)
		{
			bool ok = false;
			var snapshots = fitness.Select((fx, i) =>
			{
				var f = fx.Clone();
				if (f.SampleCount >= SampleMinimum && problem.Pools[i].UpdateBestFitness(genome, f))
				{
					ok = true;
					OnEmittingGenome(problem, genome, i, f);
				}
				return f;
			}).ToArray();

			if (ok)
			{
				var sb = new StringBuilder();
				sb.AppendLine("Genome:").AppendLine(genome.Hash);

				var asReduced = genome is IReducibleGenome<TGenome> r ? r.AsReduced() : genome;
				if (!asReduced.Equals(genome))
					sb.AppendLine("Reduced:").AppendLine(asReduced.Hash);

				for (var i = 0; i < snapshots.Length; i++)
					sb.AppendLine(FitnessScoreWithLabels(problem, i, snapshots[i]));

				lock (SynchronizedConsole.Sync)
				{
					SynchronizedConsole.OverwriteIfSame(ref LastTopGenomeUpdate, () => LastHash == genome.Hash,
					cursor =>
					{
						LastHash = genome.Hash;
						System.Console.Write(sb.AppendLine().ToString());
					});
				}
			}

			return ok;
		}

		protected virtual void OnEmittingGenome(IProblem<TGenome> p, TGenome genome, int poolIndex, Fitness fitness)
			=> LogFile?.AddLine($"{DateTime.Now},{p.ID}.{poolIndex},{p.TestCount},{fitness.Results.Average.Span.ToStringBuilder(',')},");

		public static string FitnessScoreWithLabels(IProblem<TGenome> problem, int poolIndex, Fitness fitness)
			=> $"{problem.ID}.{poolIndex}:\t{fitness}";

	}
}
