using Open.Disposable;
using Open.Text;
using Open.Threading;
using System.Collections.Concurrent;
using System.Text;

namespace Solve.Experiment.Console;

public class ConsoleEmitterBase<TGenome>(uint sampleMinimum = 50, string? logFilePath = null)
	where TGenome : class, IGenome
{
	public AsyncFileWriter? LogFile { get; } = logFilePath is null ? null : new AsyncFileWriter(logFilePath, 1000);
	public uint SampleMinimum { get; } = sampleMinimum;

	private CursorRange? _lastTopGenomeUpdate;
	public CursorRange? LastTopGenomeUpdate => _lastTopGenomeUpdate;
	protected const string BLANK = "           ";
	private readonly ConcurrentQueue<(IProblem<TGenome> problem, TGenome genome, int poolIndex, Fitness fitness)> ConsoleQueue = new();

	public void EmitTopGenomeStats((TGenome Genome, Fitness, IProblem<TGenome> Problem, int PoolIndex) update)
	{
		// Note: it's possible to see levels (sample count) 'skipped' as some genomes are pushed to the top before being selected.
		(TGenome genome, Fitness fitness, IProblem<TGenome> problem, int poolIndex) = update;
		Fitness f = fitness.Clone();
		IProblemPool<TGenome> pool = problem.Pools[poolIndex];
		if (f.SampleCount >= SampleMinimum && pool.UpdateBestFitness(genome, f))
		{
			ConsoleQueue.Enqueue((problem, genome, poolIndex, f));
			OnEmittingGenomeFitness(problem, genome, poolIndex, f);
		}

		TryEmitConsole();
	}

	protected void TryEmitConsole()
	{
	retry:
		bool locked = ThreadSafety.TryLock(SynchronizedConsole.Sync, () =>
		{
			using RecycleHelper<Dictionary<string, (IProblem<TGenome> problem, TGenome genome, int poolIndex, Fitness fitness)>> dR = DictionaryPool<string, (IProblem<TGenome> problem, TGenome genome, int poolIndex, Fitness fitness)>.Rent();
			Dictionary<string, (IProblem<TGenome> problem, TGenome genome, int poolIndex, Fitness fitness)> d = dR.Item;
			using RecycleHelper<StringBuilder> lease = StringBuilderPool.Rent();
			StringBuilder output = lease.Item;

			while (ConsoleQueue.TryDequeue(out (IProblem<TGenome> problem, TGenome genome, int poolIndex, Fitness fitness) o1))
			{
				{
					d[$"{o1.problem.ID}.{o1.poolIndex}"] = o1;
				}

				while (ConsoleQueue.TryDequeue(out (IProblem<TGenome> problem, TGenome genome, int poolIndex, Fitness fitness) o2))
				{
					d[$"{o2.problem.ID}.{o2.poolIndex}"] = o2;
				}

				try
				{
					foreach (IGrouping<TGenome, KeyValuePair<string, (IProblem<TGenome> problem, TGenome genome, int poolIndex, Fitness fitness)>> g in d
						.OrderBy(kvp => kvp.Key)
						.GroupBy(kvp => kvp.Value.genome))
					{
						OnEmittingGenome(g.Key, output);
						foreach (KeyValuePair<string, (IProblem<TGenome> problem, TGenome genome, int poolIndex, Fitness fitness)> entry in g)
						{
							(IProblem<TGenome> problem, TGenome _, int poolIndex, Fitness fitness) = entry.Value;
							output.AppendLine(FitnessScoreWithLabels(problem, poolIndex, fitness));
						}
					}

					output.AppendLine();
					SynchronizedConsole.Write(ref _lastTopGenomeUpdate,
						_ => System.Console.Write(output.ToString()));
				}
				finally
				{
					d.Clear();
					output.Clear();
				}
			}
		});

		if (locked && !ConsoleQueue.IsEmpty)
			goto retry;
	}

	protected virtual void OnEmittingGenome(
		TGenome genome,
		StringBuilder output) => output.Append("Genome:").AppendLine(BLANK).AppendLine(genome.Hash);//var asReduced = genome is IReducibleGenome<TGenome> r ? r.AsReduced() : genome;//if (!asReduced.Equals(genome))//	sb.Append("Reduced:").AppendLine(BLANK).AppendLine(asReduced.Hash);

	// ReSharper disable once UnusedParameter.Global
	// ReSharper disable once VirtualMemberNeverOverridden.Global
	protected virtual void OnEmittingGenomeFitness(IProblem<TGenome> p, TGenome genome, int poolIndex, Fitness fitness)
		=> LogFile?.AddLine($"{DateTime.Now},{p.ID}.{poolIndex},{p.TestCount},{fitness.Results.Average.ToStringBuilder(',')},");

	private static string FitnessScoreWithLabels(IProblem<TGenome> problem, int poolIndex, Fitness fitness)
		=> $"{problem.ID}.{poolIndex}:\t{fitness}";
}
