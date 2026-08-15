using System.Collections.Immutable;
using System.Reactive.Subjects;
using System.Text.Json;

namespace Solve.Tests;

public class StagnationMonitorTests
{
	private static readonly ImmutableArray<Metric> Metrics
		= [new Metric(0, "Score", "Score {0:n2}")];

	private sealed class FakeGenome(string hash) : IGenome
	{
		public bool IsFrozen => true;
		public void Freeze() { }
		public int GeneCount => 1;
		public string Hash { get; } = hash;
		public object Clone() => this;

#if DEBUG
		public string StackTrace => string.Empty;
		public IReadOnlyList<IGenomeLogEntry> Log { get; } = [];
		public void AddLogEntry(string category, string message, string? data = null) { }
#endif
	}

	private sealed class FakeProblem : IProblem<FakeGenome>
	{
		public int ID => 0;
		public IReadOnlyList<IProblemPool<FakeGenome>> Pools { get; } = [];
		public IEnumerable<Fitness> ProcessSample(FakeGenome g, long sampleId) => [];
		public ValueTask<IEnumerable<Fitness>> ProcessSampleAsync(FakeGenome g, long sampleId)
			=> new(ProcessSample(g, sampleId));
		public long TestCount => 0;
		public bool HasConverged => false;
		public void Converged() { }
	}

	private static readonly FakeProblem Problem = new();

	private static Fitness MakeFitness(double average)
	{
		var fitness = new Fitness(Metrics);
		fitness.Merge(ImmutableArray.Create(average), 1);
		return fitness;
	}

	private static (FakeGenome Genome, Fitness Fitness, IProblem<FakeGenome> Problem, int PoolIndex) Broadcast(
		string hash, double average, int poolIndex = 0)
		=> (new FakeGenome(hash), MakeFitness(average), Problem, poolIndex);

	[Fact]
	public async Task CancelsOnlyAfterFullWindowWithoutImprovement()
	{
		var subject = new Subject<(FakeGenome Genome, Fitness Fitness, IProblem<FakeGenome> Problem, int PoolIndex)>();
		int cancelCount = 0;
		var window = TimeSpan.FromMilliseconds(250);

		using var monitor = new StagnationMonitor<FakeGenome>(
			subject, window, () => Interlocked.Increment(ref cancelCount));

		subject.OnNext(Broadcast("a", 1.0));

		// Well before the window elapses: must not have cancelled yet.
		await Task.Delay(TimeSpan.FromMilliseconds(100));
		Assert.Equal(0, Volatile.Read(ref cancelCount));

		// Wait past the window with no further broadcasts (silence).
		await WaitUntil(() => Volatile.Read(ref cancelCount) > 0, TimeSpan.FromSeconds(5));

		Assert.Equal(1, Volatile.Read(ref cancelCount));
	}

	[Fact]
	public async Task ImprovementMidWindowResetsCountdown()
	{
		var subject = new Subject<(FakeGenome Genome, Fitness Fitness, IProblem<FakeGenome> Problem, int PoolIndex)>();
		int cancelCount = 0;
		var window = TimeSpan.FromMilliseconds(300);

		using var monitor = new StagnationMonitor<FakeGenome>(
			subject, window, () => Interlocked.Increment(ref cancelCount));

		subject.OnNext(Broadcast("a", 1.0));

		// Improve partway through the window; this should push the deadline out again.
		await Task.Delay(TimeSpan.FromMilliseconds(180));
		subject.OnNext(Broadcast("b", 2.0));

		// If the reset didn't happen, the original window (started at construction) would have
		// elapsed by ~300ms from the start; check shortly after that point that we're still alive.
		await Task.Delay(TimeSpan.FromMilliseconds(160));
		Assert.Equal(0, Volatile.Read(ref cancelCount));

		// Now let the reset window fully elapse in silence.
		await WaitUntil(() => Volatile.Read(ref cancelCount) > 0, TimeSpan.FromSeconds(5));
		Assert.Equal(1, Volatile.Read(ref cancelCount));
	}

	[Fact]
	public async Task EqualFitnessDoesNotResetCountdown()
	{
		var subject = new Subject<(FakeGenome Genome, Fitness Fitness, IProblem<FakeGenome> Problem, int PoolIndex)>();
		int cancelCount = 0;
		var window = TimeSpan.FromMilliseconds(250);

		using var monitor = new StagnationMonitor<FakeGenome>(
			subject, window, () => Interlocked.Increment(ref cancelCount));

		subject.OnNext(Broadcast("a", 1.0));

		// A "new champion" with the exact same average must not push the deadline out.
		await Task.Delay(TimeSpan.FromMilliseconds(150));
		subject.OnNext(Broadcast("a-equal", 1.0));

		// The original window (started at construction, ~250ms) should still elapse on schedule.
		await WaitUntil(() => Volatile.Read(ref cancelCount) > 0, TimeSpan.FromSeconds(5));
		Assert.Equal(1, Volatile.Read(ref cancelCount));
	}

	[Fact]
	public async Task WritesSummaryFileWithBestGenomePerPoolOnStagnation()
	{
		var subject = new Subject<(FakeGenome Genome, Fitness Fitness, IProblem<FakeGenome> Problem, int PoolIndex)>();
		string path = Path.Combine(Path.GetTempPath(), $"stagnation-summary-{Guid.NewGuid():N}.json");
		var window = TimeSpan.FromMilliseconds(150);

		try
		{
			using var monitor = new StagnationMonitor<FakeGenome>(
				subject, window, static () => { }, summaryFilePath: path);

			subject.OnNext(Broadcast("pool0-best", 1.0, poolIndex: 0));
			subject.OnNext(Broadcast("pool0-worse", 0.5, poolIndex: 0));
			subject.OnNext(Broadcast("pool1-best", 3.0, poolIndex: 1));

			await WaitUntil(() => monitor.LastSummary is not null, TimeSpan.FromSeconds(5));

			StagnationSummary? summary = monitor.LastSummary;
			Assert.NotNull(summary);
			Assert.Contains("Stagnat", summary!.Reason, StringComparison.OrdinalIgnoreCase);
			Assert.Equal(2, summary.Pools.Count);

			StagnationPoolSummary pool0 = summary.Pools.Single(p => p.PoolIndex == 0);
			Assert.Equal("pool0-best", pool0.BestGenomeHash);
			Assert.Equal(1.0, pool0.FitnessAverages["Score"]);

			StagnationPoolSummary pool1 = summary.Pools.Single(p => p.PoolIndex == 1);
			Assert.Equal("pool1-best", pool1.BestGenomeHash);
			Assert.Equal(3.0, pool1.FitnessAverages["Score"]);

			Assert.True(File.Exists(path));
			string json = await File.ReadAllTextAsync(path);
			StagnationSummary? fromDisk = JsonSerializer.Deserialize<StagnationSummary>(json);
			Assert.NotNull(fromDisk);
			Assert.Equal(summary.Reason, fromDisk!.Reason);
			Assert.Equal(2, fromDisk.Pools.Count);
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	[Fact]
	public async Task ExternalCancellationWritesSummaryWithoutInvokingCancelAgain()
	{
		var subject = new Subject<(FakeGenome Genome, Fitness Fitness, IProblem<FakeGenome> Problem, int PoolIndex)>();
		int cancelCount = 0;
		using var cts = new CancellationTokenSource();

		using var monitor = new StagnationMonitor<FakeGenome>(
			subject,
			TimeSpan.FromSeconds(30), // long enough that only the external cancel should fire in this test
			() => Interlocked.Increment(ref cancelCount),
			cts.Token);

		subject.OnNext(Broadcast("a", 1.0));

		cts.Cancel();

		await WaitUntil(() => monitor.LastSummary is not null, TimeSpan.FromSeconds(5));

		Assert.Equal(0, Volatile.Read(ref cancelCount));
		Assert.Contains("ancel", monitor.LastSummary!.Reason, StringComparison.OrdinalIgnoreCase);
	}

	private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
	{
		using var cts = new CancellationTokenSource(timeout);
		while (!condition())
		{
			if (cts.IsCancellationRequested)
				throw new TimeoutException($"Condition not met within {timeout}.");
			await Task.Delay(10);
		}
	}
}
