using Open.Disposable;
using Open.Text;
using Open.Threading;
using Spectre.Console.Rendering;
using System.Collections.Concurrent;
using System.Text;

namespace Solve.Experiment.Console;

/// <summary>
/// A snapshot of the current best-known genome for one problem/pool combination -- everything
/// task 15-0037's Spectre.Console live display needs to render one row of its per-pool stats
/// table (see <see cref="RunnerDisplay.BuildStatsTable"/>), without that renderer needing to
/// know anything about <c>TGenome</c>.
/// </summary>
/// <param name="ProblemId"><see cref="IProblem{TGenome}.ID"/> this stat belongs to.</param>
/// <param name="PoolIndex">Index into <see cref="IProblem{TGenome}.Pools"/>.</param>
/// <param name="GenomeHash">The champion genome's <see cref="IGenome.Hash"/>.</param>
/// <param name="GeneCount">The champion genome's <see cref="IGenome.GeneCount"/>.</param>
/// <param name="SampleCount">Sample count backing <paramref name="FitnessSummary"/>.</param>
/// <param name="FitnessSummary">Formatted "{problem.pool}:\t{fitness}" label, matching the text
/// previously written straight to the console by <c>ConsoleEmitterBase.TryEmitConsole</c>.</param>
/// <param name="GenomeText">Full descriptive text produced by <c>OnEmittingGenome</c> -- e.g.
/// the reduced formula for BlackBoxFunction/Multiplexer, or just the hash for Eater -- preserved
/// here so it remains available to the recent-events log even though the compact stats table
/// only shows a hash prefix.</param>
public readonly record struct TopGenomeStat(
	int ProblemId,
	int PoolIndex,
	string GenomeHash,
	int GeneCount,
	int SampleCount,
	string FitnessSummary,
	string GenomeText);

public class ConsoleEmitterBase<TGenome>(uint sampleMinimum = 50, string? logFilePath = null)
	where TGenome : class, IGenome
{
	public AsyncFileWriter? LogFile { get; } = logFilePath is null ? null : new AsyncFileWriter(logFilePath, 1000);
	public uint SampleMinimum { get; } = sampleMinimum;

	protected const string BLANK = "           ";

	private readonly ConcurrentDictionary<string, TopGenomeStat> _topGenomeStats = new();

	/// <summary>
	/// Live snapshot of the current best genome per problem/pool, keyed "{ProblemId}.{PoolIndex}".
	/// Consumed by <see cref="RunnerDisplay.BuildStatsTable"/> to build the per-pool stats table
	/// task 15-0037's Spectre.Console live display renders -- this replaces the direct
	/// <c>SynchronizedConsole.Write</c> calls this type used to make from <c>TryEmitConsole</c>.
	/// Safe to read concurrently while updates arrive from other threads.
	/// </summary>
	public IReadOnlyDictionary<string, TopGenomeStat> TopGenomeStats => _topGenomeStats;

	/// <summary>
	/// Raised synchronously, on whatever thread reported the update, whenever a genome becomes
	/// the new recorded best for its problem/pool -- i.e. a "champion announcement". RunnerBase
	/// subscribes to feed its recent-events log; tests can subscribe directly without needing a
	/// RunnerBase/IEnvironment at all.
	/// </summary>
	public event Action<string>? ChampionAnnounced;

	public void EmitTopGenomeStats((TGenome Genome, Fitness, IProblem<TGenome> Problem, int PoolIndex) update)
	{
		// Note: it's possible to see levels (sample count) 'skipped' as some genomes are pushed to the top before being selected.
		(TGenome genome, Fitness fitness, IProblem<TGenome> problem, int poolIndex) = update;
		Fitness f = fitness.Clone();
		IProblemPool<TGenome> pool = problem.Pools[poolIndex];
		if (f.SampleCount < SampleMinimum || !pool.UpdateBestFitness(genome, f))
			return;

		OnEmittingGenomeFitness(problem, genome, poolIndex, f);

		string key = $"{problem.ID}.{poolIndex}";
		string genomeText;
		using (RecycleHelper<StringBuilder> lease = StringBuilderPool.Rent())
		{
			StringBuilder output = lease.Item;
			OnEmittingGenome(genome, output);
			genomeText = output.ToString().TrimEnd();
		}

		string fitnessSummary = FitnessScoreWithLabels(problem, poolIndex, f);
		_topGenomeStats[key] = new TopGenomeStat(
			problem.ID, poolIndex, genome.Hash, genome.GeneCount, f.SampleCount, fitnessSummary, genomeText);

		ChampionAnnounced?.Invoke($"{key}: {ShortHash(genome.Hash)} ({genome.GeneCount:n0} genes, {f.SampleCount:n0} samples) {fitnessSummary}");
	}

	private static string ShortHash(string hash) => hash.Length <= 16 ? hash : string.Concat(hash.AsSpan(0, 16), "…");

	/// <summary>
	/// Extension point for problem-specific live-display content -- e.g. BlackBoxFunction's
	/// expression/gauge panels (task 15-0039) or Eater's grid view (task 15-0038). <see
	/// cref="RunnerBase{TGenome}"/> appends whatever this returns to its Spectre.Console layout
	/// (<see cref="RunnerDisplay.BuildLayout"/>) and to its redirected-output fallback (<see
	/// cref="RunnerDisplay.WritePlainStatus"/>), without RunnerBase/RunnerDisplay needing to know
	/// anything about <typeparamref name="TGenome"/>-specific state. Contributes nothing by
	/// default, so existing problems/tests are unaffected unless a subclass overrides this.
	/// </summary>
	public virtual IReadOnlyList<IRenderable> BuildExtraPanels() => [];

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
