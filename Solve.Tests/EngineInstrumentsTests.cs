using Eater;
using Solve.Metrics;
using Solve.ProcessingSchemes;
using Solve.Telemetry;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.Metrics;

namespace Solve.Tests;

/// <summary>
/// Verifies task 15-0030's richer instrument taxonomy (<see cref="EngineInstruments"/>, see
/// <c>Solve/Telemetry/InstrumentTaxonomy.md</c>): every instrument is published on the fixed,
/// well-known <see cref="EngineInstruments.MeterName"/> Meter with the documented kind, and each
/// recording/registration call site (<see cref="ProblemBase{TGenome}"/>'s evaluation counter and
/// duration histogram, <c>TowerScheme{TGenome}.Level</c>'s champion-gene-count histogram and
/// level-count gauge, <see cref="GenomeFactoryBase{TGenome}"/>'s breeding-stock and registry-size
/// gauges) actually produces measurements a <see cref="MeterListener"/> observes -- the same
/// mechanism <c>dotnet-counters</c> and an OpenTelemetry <c>MeterProvider</c> use.
/// </summary>
public class EngineInstrumentsTests
{
	/// <summary>
	/// A <see cref="MeterListener"/> scoped to <see cref="EngineInstruments.Meter"/> only,
	/// recording every <see langword="long"/> and <see langword="double"/> measurement it
	/// observes (push, via the callbacks) plus whatever <see cref="RecordObservableInstruments"/>
	/// pulls from the gauges on demand.
	/// </summary>
	private sealed class Recorder : IDisposable
	{
		// EngineInstruments' instruments are process-wide and static: enabling measurement
		// events on them means this listener observes every measurement recorded by ANY
		// currently-running test (xunit runs test classes in parallel by default), not just
		// this test's own calls, arriving on whatever thread each of those calls happens to run
		// on. ConcurrentQueue<T> (rather than List<T>) keeps concurrent Enqueue-while-enumerating
		// safe; the value-sensitive assertions below are written to tolerate the extra,
		// unrelated entries such cross-talk can add (never removes anything this test itself
		// recorded).
		private readonly MeterListener _listener = new();
		public ConcurrentQueue<(string Instrument, long Value, KeyValuePair<string, object?>[] Tags)> LongMeasurements { get; } = new();
		public ConcurrentQueue<(string Instrument, double Value)> DoubleMeasurements { get; } = new();
		public ConcurrentQueue<Instrument> PublishedInstruments { get; } = new();

		public Recorder()
		{
			_listener.InstrumentPublished = (instrument, listener) =>
			{
				if (!ReferenceEquals(instrument.Meter, EngineInstruments.Meter)) return;
				PublishedInstruments.Enqueue(instrument);
				listener.EnableMeasurementEvents(instrument);
			};
			_listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
				LongMeasurements.Enqueue((instrument.Name, value, tags.ToArray())));
			_listener.SetMeasurementEventCallback<double>((instrument, value, _, _) =>
				DoubleMeasurements.Enqueue((instrument.Name, value)));
			_listener.Start();
		}

		public void Poll() => _listener.RecordObservableInstruments();

		public void Dispose() => _listener.Dispose();
	}

	private static int? OwnerTag(KeyValuePair<string, object?>[] tags)
		=> tags.FirstOrDefault(t => t.Key == "owner").Value as int?;

	[Fact]
	public void EveryTaxonomyInstrumentIsPublishedOnTheEngineMeterWithTheDocumentedKind()
	{
		using var recorder = new Recorder();
		recorder.Poll(); // Forces observable-gauge instruments to be reported too.

		Assert.Equal(EngineInstruments.MeterName, EngineInstruments.Meter.Name);

		Instrument Find(string name) => Assert.Single(recorder.PublishedInstruments, i => i.Name == name);

		Assert.IsType<Counter<long>>(Find(EngineInstruments.EvaluationsInstrumentName));
		Assert.IsType<Histogram<double>>(Find(EngineInstruments.EvaluationDurationInstrumentName));
		Assert.IsType<Histogram<long>>(Find(EngineInstruments.ChampionGeneCountInstrumentName));
		Assert.IsType<ObservableGauge<long>>(Find(EngineInstruments.LevelCountInstrumentName));
		Assert.IsType<ObservableGauge<long>>(Find(EngineInstruments.BreedingStockInstrumentName));
		Assert.IsType<ObservableGauge<long>>(Find(EngineInstruments.RegistrySizeInstrumentName));

		Assert.Equal("ms", Find(EngineInstruments.EvaluationDurationInstrumentName).Unit);
		Assert.Equal("{gene}", Find(EngineInstruments.ChampionGeneCountInstrumentName).Unit);
	}

	[Fact]
	public void RecordEvaluationIncrementsTheCounterAndRecordsDuration()
	{
		using var recorder = new Recorder();

		EngineInstruments.RecordEvaluation(12.5);

		Assert.Contains(recorder.LongMeasurements,
			m => m.Instrument == EngineInstruments.EvaluationsInstrumentName && m.Value == 1);
		Assert.Contains(recorder.DoubleMeasurements,
			m => m.Instrument == EngineInstruments.EvaluationDurationInstrumentName && m.Value == 12.5);
	}

	[Fact]
	public void RecordChampionGeneCountRecordsIntoTheHistogram()
	{
		using var recorder = new Recorder();

		EngineInstruments.RecordChampionGeneCount(19);

		Assert.Contains(recorder.LongMeasurements,
			m => m.Instrument == EngineInstruments.ChampionGeneCountInstrumentName && m.Value == 19);
	}

	private static readonly ImmutableArray<Metric> SampleMetrics
		= [new Metric(0, "M", "M {0:n2}")];

	/// <summary>Minimal problem whose evaluation is instant, so this test stays fast and only
	/// exercises <see cref="ProblemBase{TGenome}"/>'s own instrumentation -- not a real fitness
	/// function.</summary>
	private sealed class InstantProblem() : ProblemBase<Genome>(
		4, 2, (SampleMetrics, (_, v) => new Fitness(SampleMetrics, ImmutableArray.Create(v[0]))))
	{
		protected override double[] ProcessSampleMetrics(Genome g, long sampleId) => [0.5];
	}

	[Fact]
	public void ProblemBaseProcessSampleIncrementsEvaluationsAndRecordsDuration()
	{
		using var recorder = new Recorder();
		var problem = new InstantProblem();
		var genome = Genome.Parse("1^");

		const int calls = 5;
		for (int i = 0; i < calls; i++)
			_ = problem.ProcessSample(genome, i).ToArray();

		Assert.Equal(calls, problem.TestCount);

		long total = recorder.LongMeasurements
			.Where(m => m.Instrument == EngineInstruments.EvaluationsInstrumentName)
			.Sum(m => m.Value);
		Assert.True(total >= calls, $"Expected at least {calls} evaluation increments, observed {total}.");

		int durationSamples = recorder.DoubleMeasurements
			.Count(m => m.Instrument == EngineInstruments.EvaluationDurationInstrumentName);
		Assert.True(durationSamples >= calls, $"Expected at least {calls} duration samples, observed {durationSamples}.");
		Assert.All(recorder.DoubleMeasurements.Where(m => m.Instrument == EngineInstruments.EvaluationDurationInstrumentName),
			m => Assert.True(m.Value >= 0, $"Duration must be non-negative, got {m.Value}."));
	}

	[Fact]
	public async Task ProblemBaseProcessSampleAsyncIncrementsEvaluationsAndRecordsDuration()
	{
		using var recorder = new Recorder();
		var problem = new InstantProblem();
		var genome = Genome.Parse("1^");

		_ = await problem.ProcessSampleAsync(genome, 0);

		Assert.Contains(recorder.LongMeasurements,
			m => m.Instrument == EngineInstruments.EvaluationsInstrumentName && m.Value >= 1);
		Assert.Contains(recorder.DoubleMeasurements,
			m => m.Instrument == EngineInstruments.EvaluationDurationInstrumentName);
	}

	/// <summary>A genome factory that produces a fresh, never-before-seen genome every call, so
	/// <c>Registry.Count</c> grows deterministically with each successful generation -- unlike a
	/// fixed-hash fake (see <c>NoveltySaturationTests.FakeGenome</c>), which is designed to
	/// collide on purpose.</summary>
	private sealed class SequentialGenomeFactory(CounterRegistry metrics) : GenomeFactoryBase<Genome>(metrics)
	{
		private int _n;

		// Two independently-varying digits give 81 unique "a^>b^" combinations before any
		// repeat -- comfortably more than any of this file's tests ever generate -- unlike a
		// single mod-9 digit, which would repeat its very first value on the 10th call.
		private Genome NextUnique()
		{
			int n = Interlocked.Increment(ref _n);
			int a = n % 9 + 1;
			int b = n / 9 % 9 + 1;
			return Genome.Parse($"{a}^>{b}^");
		}

		protected override Genome GenerateOneInternal() => NextUnique();
		protected override Genome MutateInternal(Genome target) => NextUnique();
		protected override Genome[] CrossoverInternal(Genome a, Genome b) => [NextUnique()];
	}

	[Fact]
	public void RegistrySizeGaugeReflectsGenomeFactoryRegistryCount()
	{
		using var recorder = new Recorder();
		var factory = new SequentialGenomeFactory(new CounterRegistry());
		int ownerTag = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(factory);

		const int toGenerate = 6;
		int produced = 0;
		for (int i = 0; i < toGenerate * 3 && produced < toGenerate; i++)
		{
			if (factory.TryGenerateNew(out _))
				produced++;
		}

		Assert.True(produced >= toGenerate, $"Expected at least {toGenerate} distinct genomes, produced {produced}.");

		recorder.Poll();

		var mine = recorder.LongMeasurements
			.Where(m => m.Instrument == EngineInstruments.RegistrySizeInstrumentName && OwnerTag(m.Tags) == ownerTag)
			.ToArray();
		Assert.Single(mine);
		Assert.Equal(produced, mine[0].Value);
	}

	[Fact]
	public void BreedingStockGaugeReflectsFactoryBreedingStockDepth()
	{
		using var recorder = new Recorder();
		var factory = new SequentialGenomeFactory(new CounterRegistry());
		int ownerTag = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(factory);

		Assert.True(factory.TryGenerateNew(out Genome? a));
		Assert.True(factory.TryGenerateNew(out Genome? b));
		Assert.True(factory.TryGenerateNew(out Genome? c));
		factory[0].EnqueueForBreeding(a!);
		factory[0].EnqueueForBreeding(b!);
		factory[0].EnqueueForBreeding(c!);

		recorder.Poll();

		var mine = recorder.LongMeasurements
			.Where(m => m.Instrument == EngineInstruments.BreedingStockInstrumentName && OwnerTag(m.Tags) == ownerTag)
			.ToArray();
		Assert.Single(mine);
		Assert.Equal(factory.MetricsSnapshot.BreedingStock, mine[0].Value);
		Assert.True(mine[0].Value >= 3, $"Expected breeding stock depth >= 3, observed {mine[0].Value}.");
	}

	/// <summary>Fast, synthetic problem for the end-to-end tower test below: no real Eater
	/// simulation, just enough of a fitness signal that selection (and therefore champion
	/// broadcasts and level creation) actually occurs.</summary>
	private sealed class SyntheticProblem() : ProblemBase<Genome>(
		4, 2, (SampleMetrics, (g, v) => new Fitness(SampleMetrics, ImmutableArray.Create(v[0] + g.GeneCount))))
	{
		protected override double[] ProcessSampleMetrics(Genome g, long sampleId) => [Random.Shared.NextDouble()];
	}

	[Fact]
	public async Task TowerSchemeLevelInstrumentsFireDuringARealRun()
	{
		// Session lesson (see task file): a TowerScheme test must set MaxConcurrentEvaluations
		// explicitly, or the default processor-count fan-out across concurrently-running test
		// instances risks thread-pool starvation.
		using var recorder = new Recorder();

		// Baseline the level-count gauge's currently-live owners before this test's own tower
		// exists, so its own row can be told apart from any other TowerScheme test's tower that
		// happens to still be alive in this process (xunit runs test classes in parallel).
		recorder.Poll();
		var baselineLevelCountOwners = recorder.LongMeasurements
			.Where(m => m.Instrument == EngineInstruments.LevelCountInstrumentName)
			.Select(m => OwnerTag(m.Tags))
			.ToHashSet();

		GenomeFactory factory = new(new CounterRegistry(), seeds: null, leftTurnDisabled: true);
		var scheme = new TowerScheme<Genome>(factory, new SchemeConfig
		{
			MaxLevels = 6,
			PoolSize = (6, 6, 0),
			MaxConcurrentEvaluations = 2,
		});
		scheme.AddProblem(new SyntheticProblem());

		var firstChampion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		scheme.Subscribe(_ => firstChampion.TrySetResult(), _ => firstChampion.TrySetResult());

		Task run = scheme.Start();
		await Task.WhenAny(firstChampion.Task, Task.Delay(TimeSpan.FromSeconds(5)));

		scheme.Cancel();
		try { await run.WaitAsync(TimeSpan.FromSeconds(10)); }
		catch (OperationCanceledException) { }
		catch (TimeoutException) { }

		recorder.Poll();
		var newLevelCountRows = recorder.LongMeasurements
			.Where(m => m.Instrument == EngineInstruments.LevelCountInstrumentName
				&& !baselineLevelCountOwners.Contains(OwnerTag(m.Tags)))
			.ToArray();
		Assert.True(newLevelCountRows.Length >= 1, "Expected a new solve.tower.level_count row for this test's own tower.");
		Assert.True(newLevelCountRows[0].Value >= 1, $"Expected at least the root level to exist, observed {newLevelCountRows[0].Value}.");

		Assert.Contains(recorder.LongMeasurements, m => m.Instrument == EngineInstruments.ChampionGeneCountInstrumentName);
	}
}
