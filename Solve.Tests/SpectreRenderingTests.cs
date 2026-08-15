using Solve.Experiment.Console;
using Spectre.Console;
using Spectre.Console.Rendering;
using Spectre.Console.Testing;
using System.Collections.Immutable;

namespace Solve.Tests;

/// <summary>
/// Verifies task 15-0037's Spectre.Console migration: <see cref="ConsoleEmitterBase{TGenome}"/>
/// no longer writes to the console itself (see the removed <c>SynchronizedConsole</c>/<c>Cursor</c>/
/// <c>CursorRange</c> cursor-math trio) and instead exposes a thread-safe snapshot
/// (<see cref="ConsoleEmitterBase{TGenome}.TopGenomeStats"/>) plus a champion-announcement event
/// that <see cref="RunnerDisplay"/>'s pure builders turn into the live display's stats table,
/// header, and recent-events panel. <see cref="RunnerDisplay"/> is deliberately independent of
/// <see cref="EnvironmentBase{TGenome}"/>/<see cref="IGenomeFactory{TGenome}"/>, so these tests
/// drive it directly with hand-built data rather than standing up a real environment/scheme.
/// </summary>
public class SpectreRenderingTests
{
	/// <summary>Minimal <see cref="IGenome"/> -- a fixed hash and gene count, nothing else.</summary>
	private sealed class FakeGenome(string hash, int geneCount) : IGenome
	{
		public bool IsFrozen => true;
		public void Freeze() { }
		public int GeneCount => geneCount;
		public string Hash { get; } = hash;
		public object Clone() => this;

#if DEBUG
		public string StackTrace => string.Empty;
		public IReadOnlyList<IGenomeLogEntry> Log { get; } = [];
		public void AddLogEntry(string category, string message, string? data = null) { }
#endif
	}

	private sealed class FakeProblemPool : IProblemPool<FakeGenome>
	{
		public ImmutableArray<Metric> Metrics { get; } = [new Metric(0, "Score", "Score {0:n2}")];
		public Func<FakeGenome, double[], Fitness> Transform { get; } = (_, values) => new Fitness([new Metric(0, "Score", "Score {0:n2}")], values);

		public (FakeGenome Genome, Fitness? Fitness) BestFitness { get; private set; }

		public bool UpdateBestFitness(FakeGenome genome, Fitness fitness)
		{
			BestFitness = (genome, fitness);
			return true; // Every reported fitness is treated as a new champion for these tests.
		}

		public RankedPool<FakeGenome> Champions { get; } = new(2);
	}

	private sealed class FakeProblem(int id, long testCount) : IProblem<FakeGenome>
	{
		public int ID { get; } = id;
		public IReadOnlyList<IProblemPool<FakeGenome>> Pools { get; } = [new FakeProblemPool()];
		public IEnumerable<Fitness> ProcessSample(FakeGenome g, long sampleId) => [];
		public ValueTask<IEnumerable<Fitness>> ProcessSampleAsync(FakeGenome g, long sampleId) => new(ProcessSample(g, sampleId));
		public long TestCount { get; } = testCount;
		public bool HasConverged => false;
		public void Converged() { }
	}

	private static Fitness MakeFitness(int sampleCount, double average = 1.0)
	{
		ImmutableArray<Metric> metrics = [new Metric(0, "Score", "Score {0:n2}")];
		var fitness = new Fitness(metrics);
		fitness.Merge(ImmutableArray.Create(average), sampleCount);
		return fitness;
	}

	[Fact]
	public void EmitTopGenomeStats_RecordsSnapshotAndRaisesChampionAnnouncement()
	{
		var emitter = new ConsoleEmitterBase<FakeGenome>(sampleMinimum: 1);
		var problem = new FakeProblem(id: 3, testCount: 4321);
		var genome = new FakeGenome("abcdef0123456789fedcba9876543210", geneCount: 137);
		Fitness fitness = MakeFitness(sampleCount: 60);

		List<string> announcements = [];
		emitter.ChampionAnnounced += announcements.Add;

		emitter.EmitTopGenomeStats((genome, fitness, problem, PoolIndex: 0));

		TopGenomeStat stat = Assert.Single(emitter.TopGenomeStats).Value;
		Assert.Equal("3.0", emitter.TopGenomeStats.Keys.Single());
		Assert.Equal(3, stat.ProblemId);
		Assert.Equal(0, stat.PoolIndex);
		Assert.Equal(genome.Hash, stat.GenomeHash);
		Assert.Equal(137, stat.GeneCount);
		Assert.Equal(60, stat.SampleCount);

		string announcement = Assert.Single(announcements);
		Assert.Contains("3.0", announcement, StringComparison.Ordinal);
		Assert.Contains("137", announcement, StringComparison.Ordinal);
	}

	[Fact]
	public void EmitTopGenomeStats_BelowSampleMinimum_IsIgnored()
	{
		var emitter = new ConsoleEmitterBase<FakeGenome>(sampleMinimum: 50);
		var problem = new FakeProblem(id: 1, testCount: 10);
		var genome = new FakeGenome("short", geneCount: 1);
		Fitness fitness = MakeFitness(sampleCount: 5); // below the 50-sample minimum

		emitter.EmitTopGenomeStats((genome, fitness, problem, PoolIndex: 0));

		Assert.Empty(emitter.TopGenomeStats);
	}

	/// <summary>
	/// Task 15-0037 AC4: drives a fake status update (elapsed time/per-problem test count) plus a
	/// champion announcement (via <see cref="ConsoleEmitterBase{TGenome}.EmitTopGenomeStats"/>)
	/// through <see cref="RunnerDisplay.BuildLayout"/> into a <see cref="TestConsole"/>, and
	/// asserts the rendered output contains the expected hash prefix, gene count, and test count.
	/// </summary>
	[Fact]
	public void BuildLayout_RenderedThroughTestConsole_ContainsHashPrefixGeneCountAndTestCount()
	{
		var emitter = new ConsoleEmitterBase<FakeGenome>(sampleMinimum: 1);
		var problem = new FakeProblem(id: 3, testCount: 4321);
		const string hash = "abcdef0123456789fedcba9876543210";
		var genome = new FakeGenome(hash, geneCount: 137);
		Fitness fitness = MakeFitness(sampleCount: 60);

		// Champion announcement.
		emitter.EmitTopGenomeStats((genome, fitness, problem, PoolIndex: 0));

		// Fake status update: what RunnerBase.CurrentProblemStats() would have gathered from a
		// real IEnvironment's Problems at this point in the run.
		(int ProblemId, long TestCount, long AvgTicks)[] problemStats = [(problem.ID, problem.TestCount, 999L)];

		IRenderable layout = RunnerDisplay.BuildLayout(
			info: "Solving Test Problem...",
			elapsed: TimeSpan.FromSeconds(12),
			problemStats: problemStats,
			noveltySaturation: 0.25,
			topGenomeStats: emitter.TopGenomeStats,
			recentEvents: ["Level created: 3.1"]);

		var console = new TestConsole();
		console.Width(200); // avoid wrapping split-second substring assertions across lines
		console.Write(layout);
		string output = console.Output;

		// Hash prefix (RunnerDisplay truncates to 16 chars for the compact stats table).
		Assert.Contains(hash[..16], output, StringComparison.Ordinal);
		// Gene count.
		Assert.Contains("137", output, StringComparison.Ordinal);
		// Test count (n0-formatted with a thousands separator, matching BuildHeader/BuildStatsTable).
		Assert.Contains(4321L.ToString("n0"), output, StringComparison.Ordinal);
		// Recent event (level created), and the run-start info banner.
		Assert.Contains("Level created: 3.1", output, StringComparison.Ordinal);
		Assert.Contains("Solving Test Problem", output, StringComparison.Ordinal);
	}

	[Fact]
	public void WritePlainStatus_RedirectedFallback_ContainsElapsedTestCountsAndTopGenomeStats()
	{
		var emitter = new ConsoleEmitterBase<FakeGenome>(sampleMinimum: 1);
		var problem = new FakeProblem(id: 7, testCount: 555);
		const string hash = "0123456789abcdef0123456789abcdef";
		var genome = new FakeGenome(hash, geneCount: 42);
		Fitness fitness = MakeFitness(sampleCount: 12);

		emitter.EmitTopGenomeStats((genome, fitness, problem, PoolIndex: 0));

		// A default TestConsole is non-interactive (Capabilities.Interactive == false) -- exactly
		// the redirected-stdout scenario task 15-0037 AC5 exercises against a real process.
		var console = new TestConsole();
		Assert.False(console.Profile.Capabilities.Interactive);

		(int ProblemId, long TestCount, long AvgTicks)[] problemStats = [(problem.ID, problem.TestCount, 3L)];

		RunnerDisplay.WritePlainStatus(
			console,
			info: "Solving Test Problem...",
			elapsed: TimeSpan.FromSeconds(5),
			problemStats: problemStats,
			noveltySaturation: 0.1,
			topGenomeStats: emitter.TopGenomeStats);

		string output = console.Output;
		Assert.Contains("555", output, StringComparison.Ordinal);
		Assert.Contains("42", output, StringComparison.Ordinal);
		Assert.Contains(hash[..16], output, StringComparison.Ordinal);
		Assert.Contains("Novelty saturation", output, StringComparison.Ordinal);
	}
}
