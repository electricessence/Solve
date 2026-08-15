using Eater;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Solve;
using Solve.Metrics;
using Solve.ProcessingSchemes;
using Solve.Telemetry;
using System.Diagnostics;
using System.Globalization;

// Headless benchmark harness for the Eater problem.
// Mirrors Eater.Console's Runner.Init() scheme/problem configuration but with
// ZERO seeds — no ideal seed, no previous winners, no injected random population.
// The factory bootstraps entirely from its own generator, so runs measure the
// system "from scratch". No cursor-based console UI, so it can run redirected
// and produce a comparable fitness-over-time record across code revisions.
//
// Usage: Eater.Benchmark [minutes=20] [csvPath=eater-benchmark.csv] [gridSize=10] [eventLogPath] [otelExporter]
//
// eventLogPath is optional and opt-in: when supplied (and non-blank), a structured JSONL run-event
// log (Solve.Telemetry.RunEventLog<Genome>; see Solve/Telemetry/RunEventLog.schema.md) is written
// alongside the CSV. Omitting it leaves behavior identical to before this argument existed.
//
// otelExporter (task 15-0030) is optional and opt-in: when supplied (and non-blank), an
// OpenTelemetry MeterProvider is stood up, subscribed to every "Solve.Metrics.*" Meter --
// Solve.Telemetry.EngineInstruments' richer taxonomy plus every Solve.Metrics.CounterRegistry
// instance this run creates. Pass "console" (or anything else non-URL) for the console exporter;
// pass an OTLP collector endpoint URL to export via OTLP instead -- the OTEL_EXPORTER_OTLP_ENDPOINT
// environment variable works too and takes the same effect without needing this argument at all.
// Omitting this argument leaves behavior identical to before it existed: no MeterProvider is ever
// created, zero extra overhead.

double minutes = args.Length > 0 ? double.Parse(args[0], CultureInfo.InvariantCulture) : 20;
string csvPath = args.Length > 1 ? args[1] : "eater-benchmark.csv";
ushort size = args.Length > 2 ? ushort.Parse(args[2], CultureInfo.InvariantCulture) : (ushort)10;
string? eventLogPath = args.Length > 3 && !string.IsNullOrWhiteSpace(args[3]) ? args[3] : null;
string? otelExporter = args.Length > 4 && !string.IsNullOrWhiteSpace(args[4]) ? args[4] : null;

const bool leftTurnDisabled = true;

Console.WriteLine("Eater.Benchmark: {0} minutes, grid {1}, csv: {2}", minutes, size, csvPath);
Console.WriteLine("Started (UTC): {0:O}", DateTime.UtcNow);

var metrics = new CounterRegistry();
var factory = new GenomeFactory(metrics, seeds: null, leftTurnDisabled: leftTurnDisabled);
var config = new SchemeConfig
{
	MaxLevels = 500,
	PoolSize = (400, 40, 2),
};

string manifestPath = Path.Combine(
	Path.GetDirectoryName(Path.GetFullPath(csvPath)) ?? Environment.CurrentDirectory,
	"manifest.json");
RunManifest manifest = RunManifestFactory.Create(schemeConfig: config, seed: null);
RunManifestFactory.Write(manifestPath, manifest);
Console.WriteLine("Manifest written: {0}", manifestPath);

var scheme = new TowerScheme<Genome>(factory, config);
var problem = Problem.CreateFitnessSecondary(size);
scheme.AddProblem(problem);

object gate = new();
double maxFfr = double.NegativeInfinity;
string? bestHash = null;
int bestGenes = 0;
int bestSamples = 0;
long championEvents = 0;
var stopwatch = Stopwatch.StartNew();

using var csv = new StreamWriter(csvPath);
csv.WriteLine("elapsed_s,event,pool,sample_count,food_found_rate,metric_1,metric_2,gene_count,test_count,hash");
void WriteRow(string kind, int pool, int sampleCount, double ffr, double m1, double m2, int geneCount, string hash)
{
	lock (gate)
	{
		csv.WriteLine(string.Create(CultureInfo.InvariantCulture,
			$"{stopwatch.Elapsed.TotalSeconds:f1},{kind},{pool},{sampleCount},{ffr:r},{m1:r},{m2:r},{geneCount},{problem.TestCount},\"{hash}\""));
		csv.Flush();
	}
}

scheme.Subscribe(
	e =>
	{
		(Genome genome, Fitness fitness, _, int poolIndex) = e;
		double ffr = double.NaN, m1 = double.NaN, m2 = double.NaN;
		int i = 0;
		// Fully qualified: OpenTelemetry.Metrics (opened above for the exporter wiring block)
		// also declares a Metric type, which would otherwise make this name ambiguous.
		foreach ((Solve.Metric metric, double value) in fitness.MetricAverages)
		{
			if (metric.ID == 0) ffr = value;
			else if (i == 1) m1 = value;
			else if (i == 2) m2 = value;
			i++;
		}

		lock (gate)
		{
			championEvents++;
			if (ffr > maxFfr)
			{
				maxFfr = ffr;
				bestHash = genome.Hash;
				bestGenes = genome.GeneCount;
				bestSamples = fitness.SampleCount;
				Console.WriteLine("[{0:hh\\:mm\\:ss}] new max Food-Found-Rate {1:p1} (genes: {2}, samples: {3}, pool: {4})",
					stopwatch.Elapsed, ffr, genome.GeneCount, fitness.SampleCount, poolIndex);
			}
		}

		WriteRow("champion", poolIndex, fitness.SampleCount, ffr, m1, m2, genome.GeneCount, genome.Hash);
	},
	ex => Console.WriteLine("[observer error] {0}", ex),
	() => Console.WriteLine("[broadcast completed]"));

// The engine core no longer prints "Level Created" itself (see ProblemTower.OnLevelCreated) --
// it only raises TowerScheme.LevelCreated. Subscribe here so this headless harness keeps
// producing the exact same log line existing tooling parses.
scheme.LevelCreated.Subscribe(e => Console.WriteLine("Level Created: {0}.{1}", e.Problem.ID, e.Level));

// ===== BEGIN opt-in structured event log (task 15-0029) =====
// Coexists with the CSV above; enabled only when eventLogPath is supplied. Attached before
// scheme.Start() so run_started (and no events are missed) -- see RunEventLog.schema.md.
// (Task 15-0030 adds more Solve/Telemetry/ wiring here; keep additions clearly delimited.)
RunEventLog<Genome>? eventLog = null;
if (eventLogPath is not null)
{
	eventLog = new RunEventLog<Genome>(eventLogPath, TimeSpan.FromSeconds(15));
	eventLog.Attach(scheme);
	Console.WriteLine("Event log enabled: {0}", eventLogPath);
}
// ===== END opt-in structured event log =====

// ===== BEGIN opt-in OpenTelemetry metrics export (task 15-0030) =====
// Subscribes to every "Solve.Metrics.*" Meter: Solve.Telemetry.EngineInstruments' fixed,
// well-known Meter (Solve.Metrics.Engine -- evaluations, evaluation duration, champion gene
// count, plus the level-count/breeding-stock/registry-size gauges) and this run's own
// Solve.Metrics.CounterRegistry-backed Meter (the GenomeFactory operator counters). Package
// references for the exporters used here live only in Eater.Benchmark.csproj -- Solve itself
// takes no exporter/OpenTelemetry dependency, only System.Diagnostics.Metrics instruments.
MeterProvider? meterProvider = null;
if (otelExporter is not null)
{
	string? otlpEndpointCandidate = otelExporter.Equals("console", StringComparison.OrdinalIgnoreCase)
		? null
		: otelExporter;
	string? otlpEndpoint = otlpEndpointCandidate ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

	MeterProviderBuilder builder = Sdk.CreateMeterProviderBuilder()
		.AddMeter("Solve.Metrics.*");

	meterProvider = (!string.IsNullOrWhiteSpace(otlpEndpoint)
			? builder.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint))
			: builder.AddConsoleExporter())
		.Build();

	Console.WriteLine("OpenTelemetry metrics export enabled: {0}",
		string.IsNullOrWhiteSpace(otlpEndpoint) ? "console exporter" : $"OTLP -> {otlpEndpoint}");
}
// ===== END opt-in OpenTelemetry metrics export =====

var done = new CancellationTokenSource();
var sampler = Task.Run(async () =>
{
	while (!done.IsCancellationRequested)
	{
		try { await Task.Delay(TimeSpan.FromSeconds(15), done.Token); }
		catch (OperationCanceledException) { break; }

		lock (gate)
		{
			Console.WriteLine("[{0:hh\\:mm\\:ss}] status: tests {1:n0}, champions {2:n0}, max FFR {3:p1}",
				stopwatch.Elapsed, problem.TestCount, championEvents, maxFfr);
		}

		WriteRow("status", -1, bestSamples, maxFfr, double.NaN, double.NaN, bestGenes, bestHash ?? "");
	}
});

var runTask = scheme.Start();
var deadline = Task.Delay(TimeSpan.FromMinutes(minutes));
Task first = await Task.WhenAny(runTask, deadline).ConfigureAwait(false);
if (first == deadline)
{
	Console.WriteLine("Time budget reached; cancelling scheme.");
	scheme.Cancel();
}
else
{
	Console.WriteLine("Scheme ended on its own (converged, stalled-complete, or faulted).");
}

done.Cancel();
await sampler.ConfigureAwait(false);

lock (gate)
{
	Console.WriteLine();
	Console.WriteLine("===== SUMMARY =====");
	Console.WriteLine("Elapsed: {0}", stopwatch.Elapsed);
	Console.WriteLine("Total tests: {0:n0}", problem.TestCount);
	Console.WriteLine("Champion broadcasts: {0:n0}", championEvents);
	Console.WriteLine("Max Food-Found-Rate: {0:p3}", maxFfr);
	Console.WriteLine("Best genome ({0} genes, {1} samples): {2}", bestGenes, bestSamples, bestHash);
	WriteRow("final", -1, bestSamples, maxFfr, double.NaN, double.NaN, bestGenes, bestHash ?? "");
}

try
{
	await runTask.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
	Console.WriteLine("Scheme shut down cleanly.");
}
catch (TimeoutException)
{
	Console.WriteLine("Scheme did not shut down within grace period (known stall behavior); exiting hard.");
}
catch (OperationCanceledException)
{
	Console.WriteLine("Scheme cancelled.");
}
catch (Exception ex)
{
	Console.WriteLine("[scheme fault] {0}", ex);
}

// Flush + close the event log (writes its terminal run_ended line if nothing already did).
eventLog?.Dispose();

// Disposing a MeterProvider flushes any pending metrics through its exporter(s) before this
// process exits -- without this, a final batch buffered by the PeriodicExportingMetricReader
// could be silently dropped.
meterProvider?.Dispose();

Environment.Exit(0);
