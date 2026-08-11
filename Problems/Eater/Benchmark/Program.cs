using App.Metrics;
using Eater;
using Solve;
using Solve.ProcessingSchemes;
using System.Diagnostics;
using System.Globalization;

// Headless benchmark harness for the Eater problem.
// Mirrors Eater.Console's Runner.Init() scheme/problem configuration but with
// ZERO seeds — no ideal seed, no previous winners, no injected random population.
// The factory bootstraps entirely from its own generator, so runs measure the
// system "from scratch". No cursor-based console UI, so it can run redirected
// and produce a comparable fitness-over-time record across code revisions.
//
// Usage: Eater.Benchmark [minutes=20] [csvPath=eater-benchmark.csv] [gridSize=10]

double minutes = args.Length > 0 ? double.Parse(args[0], CultureInfo.InvariantCulture) : 20;
string csvPath = args.Length > 1 ? args[1] : "eater-benchmark.csv";
ushort size = args.Length > 2 ? ushort.Parse(args[2], CultureInfo.InvariantCulture) : (ushort)10;

const bool leftTurnDisabled = true;

Console.WriteLine("Eater.Benchmark: {0} minutes, grid {1}, csv: {2}", minutes, size, csvPath);
Console.WriteLine("Started (UTC): {0:O}", DateTime.UtcNow);

var metrics = new MetricsBuilder().Build();
var factory = new GenomeFactory(metrics.Provider.Counter, seeds: null, leftTurnDisabled: leftTurnDisabled);
var config = new SchemeConfig
{
	MaxLevels = 500,
	PoolSize = (400, 40, 2),
};
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
		foreach ((Metric metric, double value) in fitness.MetricAverages)
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

Environment.Exit(0);
