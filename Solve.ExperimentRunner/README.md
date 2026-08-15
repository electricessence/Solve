# Solve.ExperimentRunner

Config-driven, headless batch runner for queuing multiple genetic-algorithm experiments
unattended and comparing their results afterward in one place. Instead of editing harness source
per run (as `Problems/Eater/Benchmark` and `Problems/Eater/Console` currently require), point this
at a directory of JSON experiment definitions and it runs each one sequentially, writing a champion
CSV and a summary JSON per run plus one line per run to a shared `results-index.jsonl`.

## Usage

```
dotnet run --project Solve.ExperimentRunner -- <definitionsDirectory> [resultsIndexPath]
```

- `definitionsDirectory` — directory containing one or more `*.json` experiment definitions.
  Non-`.json` files are ignored. Files are processed in ordinal file-name order.
- `resultsIndexPath` — optional. Where to append index records (see below). Defaults to
  `results-index.jsonl` in the current working directory.

Two bundled example definitions live in `examples/` (copied next to the built executable):
`eater-smoke.json` and `blackbox-smoke.json`, each configured to finish in well under two minutes
for smoke-testing the runner itself:

```
dotnet run --project Solve.ExperimentRunner -- Solve.ExperimentRunner/examples
```

Runs are sequential only — there is no parallel execution of multiple definitions, and no web UI
or dashboard. This is deliberately a batch CLI tool.

## Definition JSON schema

```jsonc
{
  // Required. Identifies this run: used to name its output artifacts and as the
  // results-index record's key.
  "name": "eater-smoke",

  // Required. Which problem to run and its problem-specific configuration.
  "problem": {
    // Required. "eater" or "blackbox" (case-insensitive; "blackboxfunction" also accepted).
    "id": "eater",

    // Eater only. Square grid side length. Optional, defaults to 10. Must be at least 2.
    "gridSize": 10,

    // BlackBoxFunction only. Required for that problem. One of the named formulas in
    // Formulas.cs: AB, F3A2BC, A2B2, SqrtA2B2, SqrtA2B2C2, SqrtA2B2A2B1.
    "formula": "SqrtA2B2A2B1",

    // BlackBoxFunction only. Samples drawn per evaluated level. Optional, defaults to 200.
    "sampleSize": 200
  },

  // Required. Champion-pool sizing triple applied to the tower scheme: pool size starts at
  // "first" and steps down by "step" per level to a floor of "minimum". All three must be
  // even numbers, "minimum" must be at least 2, and "first" must be >= "minimum" (the same
  // rules as Solve.ProcessingSchemes.SchemeConfig.PoolSizing).
  "poolSize": { "first": 400, "minimum": 40, "step": 2 },

  // Required. Maximum tower level the scheme may grow to. Must be greater than zero.
  "maxLevels": 500,

  // Required. Wall-clock time budget in minutes. The scheme is cancelled once this elapses,
  // unless it ends earlier on its own (converged, or naturally exhausted). Must be > 0.
  "durationMinutes": 20,

  // Optional. If present and > 0, the run is cancelled early when no per-pool fitness
  // improvement occurs within this many minutes (see Solve.StagnationMonitor). Omit to
  // disable stagnation-based termination.
  "stagnationMinutes": 5,

  // Optional. RNG seed for reproducibility.
  //
  // IMPORTANT: the underlying engine has no seedable-RNG injection point yet (see task
  // 10-0002-add-seedable-rng-injection, not yet landed). This field is accepted and recorded
  // for provenance (manifest.json's "Seed" field, and the run summary's "seed" /
  // "seedSupported": false fields) but does NOT currently make the run reproducible.
  "seed": 12345,

  // Required. Directory (relative to the current working directory, unless rooted) that this
  // run's artifacts are written into.
  "outputDirectory": "out/eater-500"
}
```

A malformed definition (invalid JSON, a missing required field, an unknown `problem.id`, or an
unknown `formula`) raises a clear, file-specific error and is skipped — it does not abort the rest
of the batch. See `ExperimentDefinitionReader.cs`.

## Per-run output artifacts

Written into the definition's `outputDirectory`, each prefixed with the definition's `name`:

| File | Contents |
| --- | --- |
| `<name>.manifest.json` | Run provenance: machine/runtime info, git commit, scheme config, seed (see `Solve.Telemetry.RunManifest`). |
| `<name>.csv` | One row per champion broadcast plus periodic status rows and a final row. Columns: `elapsed_s,event,pool,sample_count,gene_count,test_count,hash,metrics`. `event` is `champion`, `status`, or `final`. `metrics` is a semicolon-separated `Name=value` list (round-trip formatted), since different problems (and even different pools of the same problem) carry different metric sets — this keeps the CSV schema uniform across any problem type rather than hard-coding problem-specific columns. |
| `<name>.summary.json` | End-of-run summary: elapsed time, total tests, champion broadcast count, termination reason, seed/seedSupported, and each pool's best genome (hash, gene count, sample count, metric averages). |
| `<name>.stagnation.json` | Only written when `stagnationMinutes` is set — the `Solve.StagnationSummary` artifact from `StagnationMonitor`. |

## `results-index.jsonl`

One JSON object per line (JSON Lines), appended after each definition file is processed —
successful or not — so a whole batch can be compared without opening every individual summary:

```jsonc
{
  "name": "eater-500",
  "definitionFile": "eater-500.json",
  "durationSeconds": 1198.4,
  "totalTests": 4213880,
  "terminationReason": "TimeBudgetExpired",   // or "Converged", "Stagnated", "Ended", "Failed"
  "pools": [
    { "pool": 0, "hash": "...", "geneCount": 41, "sampleCount": 40, "metrics": { "Food-Found-Rate": 0.97, "...": 0 } }
  ],
  "error": null
}
```

For a definition that failed to parse or run, `pools` is `null`, `terminationReason` is `"Failed"`,
and `error` carries the failure message.

## Design notes

- **Zero seeds, headless**: like both benchmark harnesses this mirrors, the genome factory is
  bootstrapped with no injected seed genomes, no console UI, and produces a comparable
  fitness-over-time record via the CSV.
- **Convergence detection**: carried over from `Problems/BlackBoxFunction/Benchmark/Program.cs`
  (the newer of the two sibling harnesses), each run headlessly reimplements
  `Solve.Experiment.Console.RunnerBase`'s per-broadcast convergence check — a pool's best fitness
  is updated as champions arrive, and once every pool has converged (per each metric's configured
  tolerance) the scheme cancels itself early rather than idling out its full time budget. This
  applies uniformly to both problem types since convergence is a property of `Solve.Metric` /
  `Solve.Fitness`, not something either problem implements specially.
  - Test coverage: `Solve.Tests/ExperimentDefinitionReaderTests.cs` exercises the shared parser
    directly (valid definitions, every required-field-missing case, unknown problem id, unknown
    formula, invalid JSON). The end-to-end run loop is exercised manually via the bundled
    `examples/` definitions rather than as part of the automated test suite, since a real
    (if short) genetic run does not belong in a fast unit-test pass.
- **One engine, two problem types**: `ExperimentRun.RunAsync<TGenome>` is written once, generic
  over `TGenome : class, IGenome`, and instantiated for `Eater.Genome` and
  `Solve.Evaluation.EvalGenome<double>` from `ExperimentRun.ExecuteAsync`. There is no
  per-problem-type duplication of the run loop.
