using BlackBoxFunction;
using Eater;
using Solve.Evaluation;
using Solve.Metrics;
using Solve.ProcessingSchemes;
using Solve.Telemetry;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Solve.ExperimentRunner;

/// <summary>
/// Executes a single validated <see cref="ExperimentDefinition"/> headlessly, mirroring the
/// event/status/summary discipline of <c>Problems/Eater/Benchmark/Program.cs</c> and
/// <c>Problems/BlackBoxFunction/Benchmark/Program.cs</c>: zero seeds (the factory bootstraps from
/// scratch), a champion CSV row per broadcast plus periodic status rows, a time-budget cancel with
/// a shutdown grace period, and (new relative to the Eater harness, carried over from the
/// BlackBoxFunction one) a headless reimplementation of the per-pool convergence check so a run
/// that genuinely solves its problem stops early instead of idling out its full time budget.
/// </summary>
/// <remarks>
/// The two problem types (Eater's <see cref="Genome"/>, BlackBoxFunction's <c>EvalGenome&lt;double&gt;</c>)
/// share nothing but <see cref="IGenome"/>, so the entire harness below is written once as a
/// generic method and instantiated per problem type -- there is no duplicated run loop to keep in sync.
/// </remarks>
public static class ExperimentRun
{
	private static readonly JsonSerializerOptions SummaryJsonOptions = new() { WriteIndented = true };

	/// <summary>
	/// Runs <paramref name="definition"/> to completion (time budget, stagnation, or convergence)
	/// and writes its artifacts into <see cref="ExperimentDefinition.OutputDirectory"/>.
	/// </summary>
	/// <param name="definition">A definition already validated by <see cref="ExperimentDefinitionReader"/>.</param>
	/// <param name="definitionFileName">File name the definition was read from, recorded in artifacts for traceability.</param>
	public static Task<ExperimentIndexRecord> ExecuteAsync(ExperimentDefinition definition, string definitionFileName)
	{
		ArgumentNullException.ThrowIfNull(definition);

		CounterRegistry metrics = new();

		return definition.Problem switch
		{
			EaterExperimentProblem eater => RunAsync(
				definition,
				definitionFileName,
				// leftTurnDisabled: true and zero seeds mirror Eater.Benchmark's headless configuration.
				new GenomeFactory(metrics, seeds: null, leftTurnDisabled: true),
				Eater.Problem.CreateFitnessSecondary(eater.GridSize)),

			BlackBoxExperimentProblem blackBox => RunAsync(
				definition,
				definitionFileName,
				new NumericEvalGenomeFactory(metrics),
				BlackBoxFunction.Problem.Create(Formulas.ByName[blackBox.Formula], blackBox.SampleSize)),

			_ => throw new InvalidOperationException($"Unhandled problem definition type: {definition.Problem.GetType()}"),
		};
	}

	private static async Task<ExperimentIndexRecord> RunAsync<TGenome>(
		ExperimentDefinition definition,
		string definitionFileName,
		IGenomeFactory<TGenome> factory,
		IProblem<TGenome> problem)
		where TGenome : class, IGenome
	{
		DateTime startedUtc = DateTime.UtcNow;
		string outputDirectory = definition.OutputDirectory;
		Directory.CreateDirectory(outputDirectory);

		string csvPath = Path.Combine(outputDirectory, definition.Name + ".csv");
		string summaryPath = Path.Combine(outputDirectory, definition.Name + ".summary.json");
		string manifestPath = Path.Combine(outputDirectory, definition.Name + ".manifest.json");

		var config = new SchemeConfig
		{
			MaxLevels = definition.MaxLevels,
			PoolSize = definition.PoolSize,
		};

		RunManifest manifest = RunManifestFactory.Create(schemeConfig: config, seed: definition.Seed);
		RunManifestFactory.Write(manifestPath, manifest);

		var scheme = new TowerScheme<TGenome>(factory, config);
		scheme.AddProblem(problem);

		Console.WriteLine(
			"[{0}] {1} minutes, pool ({2},{3},{4}), maxLevels {5}",
			definition.Name, definition.DurationMinutes,
			definition.PoolSize.First, definition.PoolSize.Minimum, definition.PoolSize.Step,
			definition.MaxLevels);
		Console.WriteLine("Started (UTC): {0:O}", startedUtc);
		if (definition.Seed is { } seedValue)
		{
			Console.WriteLine(
				"Seed {0} requested but RNG seeding is not yet supported by the underlying engine; " +
				"recorded for provenance only (see manifest.json / summary 'seedSupported').", seedValue);
		}

		object gate = new();
		var bestPerPool = new Dictionary<int, (TGenome Genome, Fitness Fitness)>();
		long championEvents = 0;
		bool converged = false;

		// Guards against a known engine quirk: the task returned by scheme.Start() completing (or
		// even scheme.Cancel() + a clean WaitAsync shutdown below) does not guarantee every
		// per-level background task has quiesced -- a stray one can still broadcast a champion
		// well after this method has decided the run is over. Since this is a *sequential batch*
		// runner (unlike the single-shot benchmarks, which just hard-exit the process right after
		// their one run), a leftover broadcast from run N would otherwise land mid-way through run
		// N+1's console output and crash trying to write to run N's already-disposed CSV writer.
		// Checked and set under the same 'gate' lock the subscription body uses below, so there is
		// no window where a broadcast can observe stopped==false and then run concurrently with
		// (or after) csv's disposal.
		bool stopped = false;

		using var csv = new StreamWriter(csvPath);
		csv.WriteLine("elapsed_s,event,pool,sample_count,gene_count,test_count,hash,metrics");

		var stopwatch = Stopwatch.StartNew();

		void WriteRow(string kind, int pool, int sampleCount, int geneCount, string hash, IEnumerable<(Metric Metric, double Value)>? metricAverages)
		{
			string metricsField = metricAverages is null
				? string.Empty
				: string.Join(';', metricAverages.Select(mv =>
					string.Create(CultureInfo.InvariantCulture, $"{mv.Metric.Name}={mv.Value:r}")));

			lock (gate)
			{
				csv.WriteLine(string.Create(CultureInfo.InvariantCulture,
					$"{stopwatch.Elapsed.TotalSeconds:f1},{kind},{pool},{sampleCount},{geneCount},{problem.TestCount},\"{hash}\",\"{metricsField}\""));
				csv.Flush();
			}
		}

		// Mirrors Runner.cs / BlackBoxFunction.Benchmark: a champion must clear a minimum sample
		// count before it's allowed to update a pool's BestFitness (avoids a lucky first sample
		// looking "converged"), and convergence itself requires a higher sample count still.
		const uint bestFitnessSampleMinimum = 5;
		const uint minConvergenceSamples = 10;

		scheme.Subscribe(
			e =>
			{
				(TGenome genome, Fitness fitness, IProblem<TGenome> prob, int poolIndex) = e;

				lock (gate)
				{
					if (stopped) return;

					championEvents++;

					bool improved = !bestPerPool.TryGetValue(poolIndex, out (TGenome Genome, Fitness Fitness) current)
						|| fitness.IsSuperiorTo(current.Fitness);
					if (improved)
					{
						bestPerPool[poolIndex] = (genome, fitness);
						Console.WriteLine(
							"[{0:hh\\:mm\\:ss}] pool {1} new champion (genes: {2}, samples: {3}): {4}",
							stopwatch.Elapsed, poolIndex, genome.GeneCount, fitness.SampleCount, fitness);
					}

					// Headless reimplementation of Solve.Experiment.Console.RunnerBase's per-broadcast
					// convergence check (see BlackBoxFunction.Benchmark's Program.cs for the pattern this
					// is carried over from): with no console emitter to do this as a side effect of
					// rendering stats, update it explicitly here.
					IProblemPool<TGenome> pool = prob.Pools[poolIndex];
					if (fitness.SampleCount >= bestFitnessSampleMinimum)
						pool.UpdateBestFitness(genome, fitness.Clone());

					if (!prob.HasConverged && prob.Pools.All(p => p.BestFitness.Fitness?.HasConverged(minConvergenceSamples) ?? false))
						prob.Converged();

					if (!converged && scheme.HaveAllProblemsConverged)
					{
						converged = true;
						Console.WriteLine("[{0:hh\\:mm\\:ss}] problem converged; cancelling scheme.", stopwatch.Elapsed);
						scheme.Cancel();
					}

					WriteRow("champion", poolIndex, fitness.SampleCount, genome.GeneCount, genome.Hash, fitness.MetricAverages);
				}
			},
			ex => Console.WriteLine("[observer error] {0}", ex),
			() => Console.WriteLine("[broadcast completed]"));

		// Deliberately uses the lower-level constructor (explicit cancel delegate) rather than the
		// EnvironmentBase<TGenome> convenience one: the latter wires environment.Cancel directly,
		// giving no synchronous signal of *why* the scheme stopped. StagnationMonitor.LastSummary
		// is set later, from inside the stagnation timer's own callback -- checking it after
		// Task.WhenAny(runTask, deadline) resolves below is racy (LastSummary may not be assigned
		// yet even though the cancel it triggered has already unblocked runTask). Setting this flag
		// as the first statement of the delegate happens-before scheme.Cancel() on the same call
		// stack, which happens-before runTask can observe the cancellation, so by construction it
		// can never be seen as false for a run that stagnation actually ended.
		bool stagnated = false;
		StagnationMonitor<TGenome>? stagnationMonitor = definition.StagnationWindow is { } window
			? new StagnationMonitor<TGenome>(
				scheme, window,
				cancel: () => { lock (gate) { stagnated = true; } scheme.Cancel(); },
				cancellationToken: scheme.CancellationToken,
				summaryFilePath: Path.Combine(outputDirectory, definition.Name + ".stagnation.json"))
			: null;

		var done = new CancellationTokenSource();
		Task sampler = Task.Run(async () =>
		{
			while (!done.IsCancellationRequested)
			{
				try { await Task.Delay(TimeSpan.FromSeconds(15), done.Token).ConfigureAwait(false); }
				catch (OperationCanceledException) { break; }

				lock (gate)
				{
					Console.WriteLine(
						"[{0:hh\\:mm\\:ss}] status: tests {1:n0}, champions {2:n0}, pools reporting {3}",
						stopwatch.Elapsed, problem.TestCount, championEvents, bestPerPool.Count);
				}

				WriteRow("status", -1, 0, 0, string.Empty, null);
			}
		});

		Task runTask = scheme.Start();
		Task deadline = Task.Delay(TimeSpan.FromMinutes(definition.DurationMinutes));
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
		else if (stagnated)
		{
			Console.WriteLine("Scheme ended: no per-pool improvement within the stagnation window; cancelled.");
		}
		else
		{
			Console.WriteLine("Scheme ended on its own (stalled-complete or faulted).");
		}

		// From here on, any further champion broadcast (see the "known engine quirk" note above)
		// is a no-op: this run is considered finished and its CSV writer will be closed shortly.
		lock (gate) { stopped = true; }

		done.Cancel();
		await sampler.ConfigureAwait(false);

		string terminationReason =
			stagnated ? "Stagnated"
			: converged ? "Converged"
			: budgetExpired ? "TimeBudgetExpired"
			: "Ended";

		List<PoolResultRecord> poolResults;
		lock (gate)
		{
			Console.WriteLine();
			Console.WriteLine("===== SUMMARY: {0} =====", definition.Name);
			Console.WriteLine("Elapsed: {0}", stopwatch.Elapsed);
			Console.WriteLine("Total tests: {0:n0}", problem.TestCount);
			Console.WriteLine("Champion broadcasts: {0:n0}", championEvents);
			Console.WriteLine("Termination reason: {0}", terminationReason);

			poolResults = [.. bestPerPool
				.OrderBy(kv => kv.Key)
				.Select(kv => new PoolResultRecord(
					kv.Key,
					kv.Value.Genome.Hash,
					kv.Value.Genome.GeneCount,
					kv.Value.Fitness.SampleCount,
					kv.Value.Fitness.MetricAverages.ToDictionary(mv => mv.Metric.Name, mv => mv.Value)))];

			foreach (PoolResultRecord p in poolResults)
			{
				Console.WriteLine("  pool {0}: genes {1}, samples {2}, {3}",
					p.Pool, p.GeneCount, p.SampleCount,
					string.Join(", ", p.Metrics.Select(m => string.Create(CultureInfo.InvariantCulture, $"{m.Key}={m.Value:n4}"))));
			}

			WriteRow("final", -1, 0, 0, string.Empty, null);
		}

		stagnationMonitor?.Dispose();

		try
		{
			await runTask.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
			Console.WriteLine("Scheme shut down cleanly.");
		}
		catch (TimeoutException)
		{
			Console.WriteLine("Scheme did not shut down within grace period (known stall behavior); continuing to next definition.");
		}
		catch (OperationCanceledException)
		{
			Console.WriteLine("Scheme cancelled.");
		}
#pragma warning disable CA1031 // Do not catch general exception types: must not abort the batch over one run's shutdown fault.
		catch (Exception ex)
		{
			Console.WriteLine("[scheme fault] {0}", ex);
		}
#pragma warning restore CA1031

		DateTime completedUtc = DateTime.UtcNow;

		var summary = new ExperimentSummary(
			definition.Name,
			definitionFileName,
			startedUtc,
			completedUtc,
			stopwatch.Elapsed,
			problem.TestCount,
			championEvents,
			terminationReason,
			definition.Seed,
			SeedSupported: false,
			poolResults);

		File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, SummaryJsonOptions));

		return new ExperimentIndexRecord(
			definition.Name,
			definitionFileName,
			stopwatch.Elapsed.TotalSeconds,
			problem.TestCount,
			terminationReason,
			poolResults,
			Error: null);
	}
}
