using Solve.ProcessingSchemes;
using Solve.Telemetry;
using System.Collections.Immutable;
using System.Reactive.Subjects;
using System.Text.Json;

namespace Solve.Tests;

/// <summary>
/// Coverage for 15-0029: the opt-in structured JSONL run-event logger.
/// </summary>
/// <remarks>
/// Every test drives <see cref="RunEventLog{TGenome}"/> through its testable-core
/// <c>Attach(IObservable, IReadOnlyList{IProblem}, ...)</c> overload with synthetic
/// <see cref="Subject{T}"/> broadcasts rather than a real <see cref="TowerScheme{TGenome}"/> --
/// per the task's own guidance, this keeps the suite fast and avoids the thread-pool-starvation
/// pitfall of instantiating several real schemes in one run.
/// </remarks>
public class RunEventLogTests
{
	private static readonly ImmutableArray<Metric> Metrics =
		[new Metric(0, "Score", "Score {0:n2}"), new Metric(1, "Steps", "Steps {0:n2}")];

	private sealed class FakeGenome(string hash, int geneCount = 3) : IGenome
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

	private sealed class FakeProblem(int id) : IProblem<FakeGenome>
	{
		public int ID { get; } = id;
		public IReadOnlyList<IProblemPool<FakeGenome>> Pools { get; } = [];
		public IEnumerable<Fitness> ProcessSample(FakeGenome g, long sampleId) => [];
		public ValueTask<IEnumerable<Fitness>> ProcessSampleAsync(FakeGenome g, long sampleId)
			=> new(ProcessSample(g, sampleId));
		public long TestCount { get; set; }
		public bool HasConverged => false;
		public void Converged() { }
	}

	// Fitness.Merge takes a SUM (not a per-sample average) plus a sample count -- Average = Sum /
	// Count (see Fitness.MetricAverages). Scale by sampleCount here so callers can pass the
	// average they actually want asserted, independent of how many samples it's attributed to.
	private static Fitness MakeFitness(double score, double steps, int sampleCount = 1)
	{
		var fitness = new Fitness(Metrics);
		fitness.Merge(ImmutableArray.Create(score * sampleCount, steps * sampleCount), sampleCount);
		return fitness;
	}

	private static (FakeGenome Genome, Fitness Fitness, IProblem<FakeGenome> Problem, int PoolIndex) Broadcast(
		FakeProblem problem, string hash, double score, double steps,
		int poolIndex = 0, int sampleCount = 1, int geneCount = 3)
		=> (new FakeGenome(hash, geneCount), MakeFitness(score, steps, sampleCount), problem, poolIndex);

	private static string TempPath()
		=> Path.Combine(Path.GetTempPath(), $"run-event-log-{Guid.NewGuid():N}.jsonl");

	// File.ReadAllLines opens with FileShare.Read for itself, which Windows rejects while
	// RunEventLog's own writer still holds the file open for Write (share flags must be mutually
	// compatible with every other open handle in BOTH directions -- the writer's FileShare.Read
	// permits others to read, but a reader that only requests FileShare.Read back does not permit
	// the writer's still-open Write access, so the OS denies the reader's open). Open explicitly
	// with FileShare.ReadWrite here so polling mid-run (before Dispose) actually works.
	private static bool TryReadLines(string path, out string[] lines)
	{
		try
		{
			using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			using var reader = new StreamReader(stream);
			var list = new List<string>();
			string? line;
			while ((line = reader.ReadLine()) is not null) list.Add(line);
			lines = [.. list];
			return true;
		}
		catch (IOException)
		{
			lines = [];
			return false;
		}
	}

	// File.ReadAllLines can transiently throw IOException ("being used by another process")
	// immediately after a FileStream we just closed is released, even though RunEventLog.Dispose()
	// has already returned -- observed on Windows, most likely AV/indexer scanning a freshly
	// written temp file. Retry briefly rather than let that environmental flakiness fail the test.
	private static string[] ReadAllLinesWithRetry(string path, int maxAttempts = 40, int delayMs = 25)
	{
		for (int attempt = 1; ; attempt++)
		{
			try
			{
				return File.ReadAllLines(path);
			}
			catch (IOException) when (attempt < maxAttempts)
			{
				Thread.Sleep(delayMs);
			}
		}
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

	// ------------------------------------------------------------------
	// Constructor validation.
	// ------------------------------------------------------------------

	[Fact]
	public void ConstructorRejectsBlankPathAndNonPositiveInterval()
	{
		Assert.Throws<ArgumentException>(() => new RunEventLog<FakeGenome>("", TimeSpan.FromSeconds(1)));
		Assert.Throws<ArgumentOutOfRangeException>(() => new RunEventLog<FakeGenome>(TempPath(), TimeSpan.Zero));
		Assert.Throws<ArgumentOutOfRangeException>(() => new RunEventLog<FakeGenome>(TempPath(), TimeSpan.FromSeconds(-1)));
	}

	// ------------------------------------------------------------------
	// run_started (acceptance criterion 1 + 2).
	// ------------------------------------------------------------------

	[Fact]
	public void AttachWritesRunStartedFirstWithSchemeConfigAndProblemCount()
	{
		string path = TempPath();
		try
		{
			var championSubject = new Subject<(FakeGenome Genome, Fitness Fitness, IProblem<FakeGenome> Problem, int PoolIndex)>();
			var problems = new IProblem<FakeGenome>[] { new FakeProblem(1), new FakeProblem(2) };
			var config = new SchemeConfig { PoolSize = (10, 2, 2), MaxLevels = 7 };

			using (var log = new RunEventLog<FakeGenome>(path, TimeSpan.FromHours(1)))
			{
				log.Attach(championSubject, problems, schemeConfig: config);
			} // Dispose() -> flush.

			string[] lines = ReadAllLinesWithRetry(path);
			Assert.Equal(2, lines.Length); // run_started, run_ended.

			using JsonDocument first = JsonDocument.Parse(lines[0]);
			JsonElement root = first.RootElement;
			Assert.Equal("run_started", root.GetProperty("type").GetString());
			Assert.True(root.GetProperty("elapsed").GetDouble() >= 0);
			Assert.Equal(2, root.GetProperty("problemCount").GetInt32());
			Assert.Equal(7, root.GetProperty("schemeConfig").GetProperty("MaxLevels").GetInt32());

			using JsonDocument last = JsonDocument.Parse(lines[1]);
			Assert.Equal("run_ended", last.RootElement.GetProperty("type").GetString());
			Assert.Equal("Disposed.", last.RootElement.GetProperty("reason").GetString());
			Assert.Equal(0, last.RootElement.GetProperty("totalTests").GetInt64());
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	[Fact]
	public void DoubleAttachThrows()
	{
		string path = TempPath();
		try
		{
			var championSubject = new Subject<(FakeGenome Genome, Fitness Fitness, IProblem<FakeGenome> Problem, int PoolIndex)>();
			using var log = new RunEventLog<FakeGenome>(path, TimeSpan.FromHours(1));
			log.Attach(championSubject, []);
			Assert.Throws<InvalidOperationException>(() => log.Attach(championSubject, []));
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	// ------------------------------------------------------------------
	// champion / level_created field content (acceptance criterion 2).
	// ------------------------------------------------------------------

	[Fact]
	public void ChampionAndLevelCreatedEventsCaptureExpectedFields()
	{
		string path = TempPath();
		try
		{
			var championSubject = new Subject<(FakeGenome Genome, Fitness Fitness, IProblem<FakeGenome> Problem, int PoolIndex)>();
			var levelSubject = new Subject<(IProblem<FakeGenome> Problem, int Level)>();
			var problem = new FakeProblem(9);

			using (var log = new RunEventLog<FakeGenome>(path, TimeSpan.FromHours(1)))
			{
				log.Attach(championSubject, [problem], levelSubject);

				levelSubject.OnNext((problem, 3));
				championSubject.OnNext(Broadcast(problem, "hash-a", 0.9, 12.0, poolIndex: 1, sampleCount: 5, geneCount: 4));
			}

			string[] lines = ReadAllLinesWithRetry(path);
			Assert.Equal(4, lines.Length); // run_started, level_created, champion, run_ended.

			using JsonDocument levelDoc = JsonDocument.Parse(lines[1]);
			JsonElement levelRoot = levelDoc.RootElement;
			Assert.Equal("level_created", levelRoot.GetProperty("type").GetString());
			Assert.Equal(9, levelRoot.GetProperty("problemId").GetInt32());
			Assert.Equal(3, levelRoot.GetProperty("level").GetInt32());

			using JsonDocument championDoc = JsonDocument.Parse(lines[2]);
			JsonElement championRoot = championDoc.RootElement;
			Assert.Equal("champion", championRoot.GetProperty("type").GetString());
			Assert.Equal(9, championRoot.GetProperty("problemId").GetInt32());
			Assert.Equal(1, championRoot.GetProperty("poolIndex").GetInt32());
			Assert.Equal("hash-a", championRoot.GetProperty("genomeHash").GetString());
			Assert.Equal(4, championRoot.GetProperty("geneCount").GetInt32());
			Assert.Equal(5, championRoot.GetProperty("sampleCount").GetInt32());

			JsonElement averages = championRoot.GetProperty("fitnessAverages");
			Assert.Equal(2, averages.GetArrayLength());
			Assert.Equal("Score", averages[0].GetProperty("metric").GetString());
			Assert.Equal(0.9, averages[0].GetProperty("value").GetDouble());
			Assert.Equal("Steps", averages[1].GetProperty("metric").GetString());
			Assert.Equal(12.0, averages[1].GetProperty("value").GetDouble());
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	// ------------------------------------------------------------------
	// run_ended reasons (acceptance criterion 2).
	// ------------------------------------------------------------------

	[Fact]
	public void RunEndedReasonIsCompletedWhenBroadcastCompletes()
	{
		string path = TempPath();
		try
		{
			var championSubject = new Subject<(FakeGenome Genome, Fitness Fitness, IProblem<FakeGenome> Problem, int PoolIndex)>();

			using (var log = new RunEventLog<FakeGenome>(path, TimeSpan.FromHours(1)))
			{
				log.Attach(championSubject, []);
				championSubject.OnCompleted();
			}

			string[] lines = ReadAllLinesWithRetry(path);
			using JsonDocument last = JsonDocument.Parse(lines[^1]);
			Assert.Equal("run_ended", last.RootElement.GetProperty("type").GetString());
			Assert.Equal("Completed.", last.RootElement.GetProperty("reason").GetString());
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	[Fact]
	public void RunEndedReasonIsCancelledWhenTokenIsCancelled()
	{
		string path = TempPath();
		try
		{
			var championSubject = new Subject<(FakeGenome Genome, Fitness Fitness, IProblem<FakeGenome> Problem, int PoolIndex)>();
			using var cts = new CancellationTokenSource();

			using (var log = new RunEventLog<FakeGenome>(path, TimeSpan.FromHours(1)))
			{
				log.Attach(championSubject, [], cancellationToken: cts.Token);
				cts.Cancel();
			}

			string[] lines = ReadAllLinesWithRetry(path);
			using JsonDocument last = JsonDocument.Parse(lines[^1]);
			Assert.Equal("run_ended", last.RootElement.GetProperty("type").GetString());
			Assert.Equal("Cancelled.", last.RootElement.GetProperty("reason").GetString());
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	[Fact]
	public void BroadcastErrorWritesFaultThenFaultedRunEndedExactlyOnce()
	{
		string path = TempPath();
		try
		{
			var championSubject = new Subject<(FakeGenome Genome, Fitness Fitness, IProblem<FakeGenome> Problem, int PoolIndex)>();

			using (var log = new RunEventLog<FakeGenome>(path, TimeSpan.FromHours(1)))
			{
				log.Attach(championSubject, []);
				championSubject.OnError(new InvalidOperationException("boom"));
			} // Dispose()'s own TryWriteRunEnded("Disposed.") must be a no-op: only one run_ended line.

			string[] lines = ReadAllLinesWithRetry(path);
			Assert.Equal(3, lines.Length); // run_started, fault, run_ended.

			using JsonDocument faultDoc = JsonDocument.Parse(lines[1]);
			JsonElement faultRoot = faultDoc.RootElement;
			Assert.Equal("fault", faultRoot.GetProperty("type").GetString());
			Assert.Equal("boom", faultRoot.GetProperty("message").GetString());
			Assert.Equal("championBroadcast", faultRoot.GetProperty("source").GetString());

			using JsonDocument endedDoc = JsonDocument.Parse(lines[2]);
			JsonElement endedRoot = endedDoc.RootElement;
			Assert.Equal("run_ended", endedRoot.GetProperty("type").GetString());
			Assert.Contains("boom", endedRoot.GetProperty("reason").GetString());

			Assert.Single(lines, l => l.Contains("\"run_ended\"", StringComparison.Ordinal));
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	// ------------------------------------------------------------------
	// status sampling (acceptance criterion 2).
	// ------------------------------------------------------------------

	[Fact]
	public async Task StatusEventSamplesTestCountsAndChampionCount()
	{
		string path = TempPath();
		try
		{
			var championSubject = new Subject<(FakeGenome Genome, Fitness Fitness, IProblem<FakeGenome> Problem, int PoolIndex)>();
			var p1 = new FakeProblem(1);
			var p2 = new FakeProblem(2);

			using var log = new RunEventLog<FakeGenome>(path, TimeSpan.FromMilliseconds(30));
			log.Attach(championSubject, [p1, p2]);

			championSubject.OnNext(Broadcast(p1, "hash-a", 0.5, 1.0));
			p1.TestCount = 77;
			p2.TestCount = 5;

			// Generous timeout: the periodic status timer's ThreadPool-queued callback can be
			// delayed well past its 30ms interval when the machine is under heavy concurrent load
			// (e.g. other test processes), so this bounds worst-case wait rather than expected wait.
			await WaitUntil(
				() => TryReadLines(path, out string[] lines) && lines.Any(l => l.Contains("\"status\"", StringComparison.Ordinal)),
				TimeSpan.FromSeconds(15));

			log.Dispose();

			string[] finalLines = ReadAllLinesWithRetry(path);
			// At least one status tick is guaranteed by the wait above; more than one is fine --
			// the 30ms interval may have ticked again before Dispose() stopped the timer. Assert
			// against the last one (it reflects the final TestCount/championCount values).
			string statusLine = finalLines.Last(l => l.Contains("\"status\"", StringComparison.Ordinal));
			using JsonDocument statusDoc = JsonDocument.Parse(statusLine);
			JsonElement statusRoot = statusDoc.RootElement;

			JsonElement counts = statusRoot.GetProperty("testCounts");
			Dictionary<int, long> byProblem = counts.EnumerateArray()
				.ToDictionary(e => e.GetProperty("problemId").GetInt32(), e => e.GetProperty("testCount").GetInt64());
			Assert.Equal(77, byProblem[1]);
			Assert.Equal(5, byProblem[2]);
			Assert.True(statusRoot.GetProperty("championCount").GetInt64() >= 1);
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	// ------------------------------------------------------------------
	// Non-blocking writes + flush-on-dispose (acceptance criterion 5).
	// ------------------------------------------------------------------

	[Fact]
	public void DisposeFlushesAllEnqueuedEventsWithNoPriorWait()
	{
		string path = TempPath();
		try
		{
			var championSubject = new Subject<(FakeGenome Genome, Fitness Fitness, IProblem<FakeGenome> Problem, int PoolIndex)>();
			var problem = new FakeProblem(1);
			const int championEventCount = 200;

			using (var log = new RunEventLog<FakeGenome>(path, TimeSpan.FromHours(1)))
			{
				log.Attach(championSubject, [problem]);

				// Push a burst of events back-to-back with no delay/yield at all, then dispose
				// immediately: Dispose must not return until every one of these has reached disk
				// (the whole point of "flushed on dispose"), proving the channel hand-off is not
				// merely "usually fast enough" but actually synchronized.
				for (int i = 0; i < championEventCount; i++)
					championSubject.OnNext(Broadcast(problem, $"hash-{i}", i, i * 2.0));
			}

			string[] lines = ReadAllLinesWithRetry(path);
			// run_started + N champions + run_ended.
			Assert.Equal(championEventCount + 2, lines.Length);

			int championLines = lines.Count(l => l.Contains("\"champion\"", StringComparison.Ordinal));
			Assert.Equal(championEventCount, championLines);

			// Every single line must be independently valid JSON (JSONL contract).
			foreach (string line in lines)
			{
				using JsonDocument doc = JsonDocument.Parse(line);
				Assert.True(doc.RootElement.TryGetProperty("type", out _));
				Assert.True(doc.RootElement.TryGetProperty("elapsed", out _));
			}
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	// ------------------------------------------------------------------
	// Full synthetic run covering all six event types (acceptance criterion 4).
	// ------------------------------------------------------------------

	[Fact]
	public async Task SyntheticRunProducesParseableJsonlWithAllSixEventTypes()
	{
		string path = TempPath();
		try
		{
			var championSubject = new Subject<(FakeGenome Genome, Fitness Fitness, IProblem<FakeGenome> Problem, int PoolIndex)>();
			var levelSubject = new Subject<(IProblem<FakeGenome> Problem, int Level)>();
			var problem = new FakeProblem(1);
			var config = new SchemeConfig();

			using var log = new RunEventLog<FakeGenome>(path, TimeSpan.FromMilliseconds(30));
			log.Attach(championSubject, [problem], levelSubject, config);

			levelSubject.OnNext((problem, 1));
			championSubject.OnNext(Broadcast(problem, "hash-a", 0.5, 1.0));
			problem.TestCount = 42;

			await WaitUntil(
				() => TryReadLines(path, out string[] lines) && lines.Any(l => l.Contains("\"status\"", StringComparison.Ordinal)),
				TimeSpan.FromSeconds(15));

			championSubject.OnError(new InvalidOperationException("synthetic fault"));

			log.Dispose();

			string[] lines = ReadAllLinesWithRetry(path);
			Assert.NotEmpty(lines);

			var requiredFieldsByType = new Dictionary<string, string[]>
			{
				["run_started"] = ["startedUtc", "problemCount"],
				["level_created"] = ["problemId", "level"],
				["champion"] = ["problemId", "poolIndex", "genomeHash", "geneCount", "sampleCount", "fitnessAverages"],
				["status"] = ["testCounts", "championCount"],
				["fault"] = ["message"],
				["run_ended"] = ["reason", "totalTests"],
			};

			var typesSeen = new HashSet<string>();
			foreach (string line in lines)
			{
				using JsonDocument doc = JsonDocument.Parse(line); // must not throw: every line is standalone valid JSON.
				JsonElement root = doc.RootElement;

				string type = root.GetProperty("type").GetString()
					?? throw new Xunit.Sdk.XunitException("Missing 'type'.");
				Assert.True(root.GetProperty("elapsed").GetDouble() >= 0);

				Assert.True(requiredFieldsByType.TryGetValue(type, out string[]? requiredFields), $"Unknown event type '{type}'.");
				foreach (string field in requiredFields!)
					Assert.True(root.TryGetProperty(field, out _), $"'{type}' line missing required field '{field}': {line}");

				typesSeen.Add(type);
			}

			Assert.Equal(requiredFieldsByType.Keys.ToHashSet(), typesSeen);
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}
}
