using Open.Text;
using Open.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace Solve.Experiment.Console
{
	public class ConsoleEmitterBase<TGenome>
		where TGenome : class, IGenome
	{
		public readonly AsyncFileWriter LogFile;
		public readonly uint SampleMinimum;

		// ReSharper disable once MemberCanBeProtected.Global
		public ConsoleEmitterBase(uint sampleMinimum = 50, string logFilePath = null)
		{
			LogFile = logFilePath == null ? null : new AsyncFileWriter(logFilePath, 1000);
			SampleMinimum = sampleMinimum;
		}

		public CursorRange LastTopGenomeUpdate;

		protected const string BLANK = "           ";

		readonly ConcurrentQueue<(IProblem<TGenome> problem, TGenome genome, int poolIndex, Fitness fitness)> ConsoleQueue
			= new ConcurrentQueue<(IProblem<TGenome> problem, TGenome genome, int poolIndex, Fitness fitness)>();

		[SuppressMessage("ReSharper", "ImplicitlyCapturedClosure")]
		public void EmitTopGenomeStats(IProblem<TGenome> problem, (TGenome genome, int poolIndex, Fitness fitness) update)
		{
			// Note: it's possible to see levels (sample count) 'skipped' as some genomes are pushed to the top before being selected.
			var (genome, poolIndex, fitness) = update;
			var f = fitness.Clone();
			var pool = problem.Pools[poolIndex];
			if (f.SampleCount >= SampleMinimum && pool.UpdateBestFitness(genome, f))
			{
				ConsoleQueue.Enqueue((problem, genome, poolIndex, f));
				OnEmittingGenomeFitness(problem, genome, poolIndex, f);
			}

			TryEmitConsole();
		}

		protected void TryEmitConsole()
		{
			ThreadSafety.TryLock(SynchronizedConsole.Sync,
				() =>
				{
					while (ConsoleQueue.TryDequeue(out var o1))
					{
						var d = new Dictionary<string, (IProblem<TGenome> problem, TGenome genome, int poolIndex, Fitness fitness)>();
						{
							d[$"{o1.problem.ID}.{o1.poolIndex}"] = o1;
						}

						while (ConsoleQueue.TryDequeue(out var o2))
						{
							d[$"{o2.problem.ID}.{o2.poolIndex}"] = o2;
						}


						var output = StringBuilderPool.Instance.Take();
						try
						{
							foreach (var g in d.OrderBy(kvp => kvp.Key).GroupBy(kvp => kvp.Value.genome))
							{
								OnEmittingGenome(g.Key, output);
								foreach (var entry in g)
								{
									var (problem, _, poolIndex, fitness) = entry.Value;
									output.AppendLine(FitnessScoreWithLabels(problem, poolIndex, fitness));
								}
							}

							output.AppendLine();
							SynchronizedConsole.Write(ref LastTopGenomeUpdate,
								cursor =>
								{
									System.Console.Write(output.ToString());
								});
						}
						finally
						{
							StringBuilderPool.Instance.Give(output);
						}


					}
				});
		}

		[SuppressMessage("ReSharper", "UnusedParameter.Global")]
		protected virtual void OnEmittingGenome(
			TGenome genome,
			StringBuilder output)
		{
			output.Append("Genome:").AppendLine(BLANK).AppendLine(genome.Hash);
			//var asReduced = genome is IReducibleGenome<TGenome> r ? r.AsReduced() : genome;
			//if (!asReduced.Equals(genome))
			//	sb.Append("Reduced:").AppendLine(BLANK).AppendLine(asReduced.Hash);
		}


		// ReSharper disable once UnusedParameter.Global
		// ReSharper disable once VirtualMemberNeverOverridden.Global
		protected virtual void OnEmittingGenomeFitness(IProblem<TGenome> p, TGenome genome, int poolIndex, Fitness fitness)
			=> LogFile?.AddLine($"{DateTime.Now},{p.ID}.{poolIndex},{p.TestCount},{fitness.Results.Average.ToStringBuilder(',')},");

		public static string FitnessScoreWithLabels(IProblem<TGenome> problem, int poolIndex, Fitness fitness)
			=> $"{problem.ID}.{poolIndex}:\t{fitness}";

	}
}
