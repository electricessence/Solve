using BlackBoxFunction;
using BlackBoxFunction.Benchmark;
using Solve;
using Solve.Evaluation;
using Solve.Metrics;
using Solve.ProcessingSchemes;
using System.Diagnostics;
using System.Globalization;

// Headless benchmark harness for the BlackBoxFunction (symbolic regression) problem.
// Mirrors BlackBoxFunction.Runner's Init() scheme/problem configuration but with
// ZERO seeds — no injected seed genomes. The factory bootstraps entirely from its own
// generator, so runs measure the system "from scratch". No cursor-based console UI, so it
// can run redirected and produce a comparable fitness-over-time record across code revisions.
//
// Unlike Eater, BlackBoxFunction's metrics carry convergence tolerances (see Problem.Metrics01),
// so a run can succeed outright: this harness reimplements RunnerBase's per-pool convergence
// check headlessly (no console, no ConsoleEmitterBase) and self-terminates when the problem
// converges, in addition to the usual time-budget cancellation.
//
// Usage: BlackBox.Benchmark [minutes=20] [csvPath=blackbox-benchmark.csv] [formula=SqrtA2B2A2B1] [fitConstants=false]
//
// 25-0026: the trailing "fitConstants" flag is the harness-side half of the constants-fitting A/B
// switch -- it just forwards to NumericEvalGenomeFactory.ConstantsFittingEnabled (see
// Solve.Evaluation/NumericEvalGenomeFactory.cs and Solve.Evaluation/ConstantsFitting.cs).

double minutes = args.Length > 0 ? double.Parse(args[0], CultureInfo.InvariantCulture) : 20;
string csvPath = args.Length > 1 ? args[1] : "blackbox-benchmark.csv";
string formulaName = args.Length > 2 ? args[2] : "SqrtA2B2A2B1";
bool fitConstants = args.Length > 3 && bool.Parse(args[3]);

if (!Formulas.ByName.TryGetValue(formulaName, out Formula? formula))
{
	Console.WriteLine("Unknown formula '{0}'. Available: {1}", formulaName, string.Join(", ", Formulas.ByName.Keys));
	Environment.Exit(1);
	return;
}

const ushort sampleSize = 200;

// Mirrors Runner.cs: new Runner(5) => minSamples=5 feeds the emitter's SampleMinimum (gate for
// updating a pool's BestFitness); RunnerBase's ctor takes max(minSamples, minConvSamples=10) as
// the convergence sample threshold, so effectively 10 here since 5 is not greater than 10.
const uint bestFitnessSampleMinimum = 5;
const uint minConvergenceSamples = 10;

Console.WriteLine("BlackBox.Benchmark: {0} minutes, formula {1}, csv: {2}, fitConstants: {3}", minutes, formulaName, csvPath, fitConstants);
Console.WriteLine("Started (UTC): {0:O}", DateTime.UtcNow);

var metrics = new CounterRegistry();
var factory = new NumericEvalGenomeFactory(metrics) { ConstantsFittingEnabled = fitConstants }; // zero seeds; 25-0026 opt-in switch
var scheme = new TowerScheme<EvalGenome<double>>(factory, (800, 80, 2));
var problem = Problem.Create(formula, sampleSize);
scheme.AddProblem(problem);

object gate = new();
double maxCorrelation = double.NegativeInfinity;
double bestDivergence = double.NaN;
double bestDirection = double.NaN;
string? bestHash = null;
int bestGenes = 0;
int bestSamples = 0;
long championEvents = 0;
bool converged = false;

// 25-0026: fitting is applied selectively (from the champion broadcast below), never on every
// evaluation, and at most one fit runs at a time -- fittingBusy is a cheap in-flight guard so a
// burst of champion broadcasts can't pile up unbounded background work; excess broadcasts during
// that window are simply skipped rather than queued.
const long ConstantsFitSampleId = -1; // Distinct from the problem's own per-round sample ids; deterministic/cached by SampleCache2.
int fittingBusy = 0;
long constantsFitAttempts = 0;
long constantsFitRegistered = 0;
TimeSpan? convergedAt = null;
var stopwatch = Stopwatch.StartNew();

using var csv = new StreamWriter(csvPath);
csv.WriteLine("elapsed_s,event,pool,sample_count,direction,correlation,divergence,gene_count,test_count,hash");
void WriteRow(string kind, int pool, int sampleCount, double direction, double correlation, double divergence, int geneCount, string hash)
{
	lock (gate)
	{
		csv.WriteLine(string.Create(CultureInfo.InvariantCulture,
			$"{stopwatch.Elapsed.TotalSeconds:f1},{kind},{pool},{sampleCount},{direction:r},{correlation:r},{divergence:r},{geneCount},{problem.TestCount},\"{hash}\""));
		csv.Flush();
	}
}

scheme.Subscribe(
	e =>
	{
		(EvalGenome<double> genome, Fitness fitness, IProblem<EvalGenome<double>> prob, int poolIndex) = e;
		double direction = double.NaN, correlation = double.NaN, divergence = double.NaN;
		foreach ((Metric metric, double value) in fitness.MetricAverages)
		{
			switch (metric.ID)
			{
				case 0: direction = value; break;
				case 1: correlation = value; break;
				case 2: divergence = value; break;
			}
		}

		lock (gate)
		{
			championEvents++;
			if (correlation > maxCorrelation)
			{
				maxCorrelation = correlation;
				bestDivergence = divergence;
				bestDirection = direction;
				bestHash = genome.Hash;
				bestGenes = genome.GeneCount;
				bestSamples = fitness.SampleCount;
				Console.WriteLine("[{0:hh\\:mm\\:ss}] new max correlation {1:p6} (divergence: {2:n3}, genes: {3}, samples: {4}, pool: {5})",
					stopwatch.Elapsed, correlation, divergence, genome.GeneCount, fitness.SampleCount, poolIndex);
			}

			// Headless reimplementation of Solve.Experiment.Console.RunnerBase's per-broadcast
			// convergence check (see RunnerBase.Start): normally ConsoleEmitterBase.EmitTopGenomeStats
			// keeps each pool's BestFitness current (gated by a minimum sample count) as a side effect
			// of rendering console stats; with no console emitter here we do that update explicitly.
			IProblemPool<EvalGenome<double>> pool = prob.Pools[poolIndex];
			if (fitness.SampleCount >= bestFitnessSampleMinimum)
				pool.UpdateBestFitness(genome, fitness.Clone());

			if (!prob.HasConverged && prob.Pools.All(p => p.BestFitness.Fitness?.HasConverged(minConvergenceSamples) ?? false))
				prob.Converged();

			if (!converged && scheme.HaveAllProblemsConverged)
			{
				converged = true;
				convergedAt = stopwatch.Elapsed;
				Console.WriteLine("[{0:hh\\:mm\\:ss}] problem converged; cancelling scheme.", stopwatch.Elapsed);
				scheme.Cancel();
			}
		}

		// 25-0026: constants-fitting pass, applied selectively from this champion broadcast rather
		// than every evaluation. At most one fit runs at a time (fittingBusy guards this); a burst of
		// champions while a fit is in flight just skips extra dispatches instead of queuing unbounded
		// background work, keeping the added cost sublinear. Offloaded via Task.Run so the fit (bounded
		// to ConstantsFitting.DefaultTimeBudget) never blocks the tower's evaluation worker thread that
		// is broadcasting this champion.
		if (factory.ConstantsFittingEnabled && Interlocked.CompareExchange(ref fittingBusy, 1, 0) == 0)
		{
			Interlocked.Increment(ref constantsFitAttempts);
			EvalGenome<double> championGenome = genome;
			_ = Task.Run(() =>
			{
				try
				{
					// SampleCache2.Entry.Values is deliberately an "endless" LazyList (see
					// BlackBoxSampleTests.cs) -- its .Count throws until drained by indexing, so it
					// must be materialized via the indexer (never via .Count/enumeration) before being
					// handed to ConstantsFitting.Fit, which does check samples.Count up front.
					SampleCache2.Entry fitEntry = problem.Samples.Get(ConstantsFitSampleId);
					var fitSamples = new (IReadOnlyList<double> Input, double Target)[problem.Samples.SampleSize];
					for (int i = 0; i < fitSamples.Length; i++)
						fitSamples[i] = fitEntry[i];

					EvalGenome<double>? fitted = factory.TryFitConstants(championGenome, fitSamples);
					if (fitted is not null)
						Interlocked.Increment(ref constantsFitRegistered);
				}
				catch (Exception ex)
				{
					Console.WriteLine("[constants-fit error] {0}", ex.Message);
				}
				finally
				{
					Interlocked.Exchange(ref fittingBusy, 0);
				}
			});
		}

		WriteRow("champion", poolIndex, fitness.SampleCount, direction, correlation, divergence, genome.GeneCount, genome.Hash);
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
			Console.WriteLine("[{0:hh\\:mm\\:ss}] status: tests {1:n0}, champions {2:n0}, max correlation {3:p6}",
				stopwatch.Elapsed, problem.TestCount, championEvents, maxCorrelation);
		}

		WriteRow("status", -1, bestSamples, bestDirection, maxCorrelation, bestDivergence, bestGenes, bestHash ?? "");
	}
});

var runTask = scheme.Start();
var deadline = Task.Delay(TimeSpan.FromMinutes(minutes));
Task first = await Task.WhenAny(runTask, deadline).ConfigureAwait(false);
bool budgetExpired = !converged && first == deadline;
if (budgetExpired)
{
	Console.WriteLine("Time budget reached; cancelling scheme.");
	scheme.Cancel();
}
else if (converged)
{
	Console.WriteLine("Scheme ended: problem converged.");
}
else
{
	Console.WriteLine("Scheme ended on its own (stalled-complete or faulted).");
}

done.Cancel();
await sampler.ConfigureAwait(false);

// 25-0026: give a single possibly-in-flight constants-fit background task (fittingBusy caps this
// at one) a brief bounded chance to finish so the summary counts below reflect it.
if (fitConstants)
{
	DateTime drainDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
	while (Interlocked.CompareExchange(ref fittingBusy, 0, 0) != 0 && DateTime.UtcNow < drainDeadline)
		await Task.Delay(20).ConfigureAwait(false);
}

lock (gate)
{
	string status = converged ? "CONVERGED" : budgetExpired ? "BUDGET-EXPIRED" : "ENDED";

	Console.WriteLine();
	Console.WriteLine("===== SUMMARY =====");
	Console.WriteLine("Formula: {0}", formulaName);
	Console.WriteLine("Elapsed: {0}", stopwatch.Elapsed);
	Console.WriteLine("Total tests: {0:n0}", problem.TestCount);
	Console.WriteLine("Tests/second: {0:n1}", problem.TestCount / stopwatch.Elapsed.TotalSeconds);
	Console.WriteLine("Champion broadcasts: {0:n0}", championEvents);
	Console.WriteLine("Constants-fit enabled: {0}", fitConstants);
	Console.WriteLine("Constants-fit attempts: {0:n0}", constantsFitAttempts);
	Console.WriteLine("Constants-fit variants registered: {0:n0}", constantsFitRegistered);
	Console.WriteLine("Max correlation: {0:p6}", maxCorrelation);
	Console.WriteLine("Best divergence: {0:n6}", bestDivergence);
	Console.WriteLine("Best genome ({0} genes, {1} samples): {2}", bestGenes, bestSamples, bestHash);
	Console.WriteLine("Convergence status: {0}", status);
	if (converged && convergedAt is { } ca)
		Console.WriteLine("Time to convergence: {0}", ca);
	WriteRow("final", -1, bestSamples, bestDirection, maxCorrelation, bestDivergence, bestGenes, bestHash ?? "");
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
