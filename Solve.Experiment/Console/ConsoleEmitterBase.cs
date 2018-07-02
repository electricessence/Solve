using Open.Threading;
using System;
using System.Text;

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

		//public CursorRange LastTopGenomeUpdate;

		public bool EmitTopGenomeStats(TGenome genome, (IProblem<TGenome> Problem, FitnessContainer[] Fitness)[] stats)
		{
			var sb = Lazy.Create(() =>
			{
				var s = new StringBuilder();
				var asReduced = genome is IReducibleGenome<TGenome> r ? r.AsReduced() : genome;
				if (asReduced.Equals(genome))
					s.AppendLine(genome.Hash);
				else
					s.AppendFormat("{0}\n=>{1}\n", genome.Hash, asReduced.Hash);
				return s;
			});

			var pCount = stats.Length;
			for (var j = 0; j < pCount; j++)
			{
				var (Problem, Fitness) = stats[j];

				var len = Fitness.Length;
				for (var i = 0; i < len; i++)
				{
					var f = Fitness[i].Clone();
					if (f.SampleCount >= SampleMinimum && Problem.Pools[i].UpdateBestFitness(genome, f))
					{
						sb.Value.AppendLine(FitnessScoreWithLabels(Problem, i, f));
						sb.Value.AppendLine();
						OnEmittingGenome(Problem, genome, i, f);
					}
				}
			}

			return ThreadSafety.LockConditional(
				SynchronizedConsole.Sync,
				() => sb.IsValueCreated,
				() => //SynchronizedConsole.OverwriteIfSame(ref LastTopGenomeUpdate, cursor =>
					System.Console.Write(sb.ToString()));//);
		}

		protected virtual void OnEmittingGenome(IProblem<TGenome> p, TGenome genome, int poolIndex, FitnessContainer fitness)
			=> LogFile?.AddLine($"{DateTime.Now},{p.ID}.{poolIndex},{p.TestCount},{fitness.Results.Average.Span.ToStringBuilder(',')},");

		public static string FitnessScoreWithLabels(IProblem<TGenome> problem, int poolIndex, FitnessContainer fitness)
			=> $"{problem.ID}.{poolIndex}:\t{fitness}";

	}
}
