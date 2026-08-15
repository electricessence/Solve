# Engine instrument taxonomy

Task 15-0030's `System.Diagnostics.Metrics` instrument taxonomy. Every instrument below is
published on one process-wide `Meter` named **`Solve.Metrics.Engine`** (`EngineInstruments.MeterName`,
`Solve/Telemetry/EngineInstruments.cs`) -- point `dotnet-counters monitor --counters
Solve.Metrics.Engine` (or an OpenTelemetry `MeterProvider.AddMeter("Solve.Metrics.Engine")`) at
that one name to see all six.

This is a *separate* `Meter` from `Solve.Metrics.CounterRegistry`'s (task 25-0025): that one is a
private, per-instance field -- every `new CounterRegistry()` call gets its own randomly-named
Meter (`Solve.Metrics.{Guid}` unless a caller supplies a stable name), which cannot itself serve as
a single well-known name for external tooling to address. `EngineInstruments` follows the same
`Solve.Metrics.*` naming root so the two are recognizably part of one family, but as one fixed,
process-wide instance. See the `<remarks>` on `EngineInstruments` for the full rationale.

## Instruments

| Name                             | Kind                | Unit          | Recorded / observed at                                                        | Description |
|-----------------------------------|---------------------|---------------|---------------------------------------------------------------------------------|--------------|
| `solve.evaluations`               | Counter\<long>       | `{evaluation}`| `ProblemBase<TGenome>.ProcessSample` / `ProcessSampleAsync`                     | Count of fitness-case evaluations, across every `IProblem<TGenome>` instance in the process. Always agrees with the sum of every problem's `TestCount` (same call sites increment both). |
| `solve.evaluation.duration`       | Histogram\<double>   | `ms`          | `ProblemBase<TGenome>.ProcessSample` / `ProcessSampleAsync`                     | Wall-clock duration of one evaluation -- the `ProcessSampleMetrics`/`ProcessSampleMetricsAsync` call that produces a single fitness sample. Timed via `Stopwatch.GetTimestamp()`/`GetElapsedTime` (no allocation). |
| `solve.champion.gene_count`       | Histogram\<long>     | `{gene}`      | `TowerScheme<TGenome>.Level.ProcessChampion`                                    | `Genome.GeneCount` of each champion at the moment it is broadcast. |
| `solve.tower.level_count`         | ObservableGauge\<long>| `{level}`    | Pull, on listener poll; updated at `Level`'s constructor (one per tower)        | Number of levels currently created for a running `TowerScheme<TGenome>.ProblemTower` -- one measurement per active tower. Monotonically non-decreasing for a given tower's lifetime. |
| `solve.factory.breeding_stock`    | ObservableGauge\<long>| `{genome}`   | Pull, on listener poll; source registered in `GenomeFactoryBase<TGenome>`'s constructor | Current breeding-stock depth (`GenomeFactoryMetrics.BreedingStock`, read via the factory's existing `MetricsSnapshot`) -- one measurement per active `GenomeFactoryBase<TGenome>`. |
| `solve.factory.registry_size`     | ObservableGauge\<long>| `{genome}`   | Pull, on listener poll; source registered in `GenomeFactoryBase<TGenome>`'s constructor | Current genome registry size (`GenomeFactoryBase<TGenome>.Registry.Count`) -- one measurement per active `GenomeFactoryBase<TGenome>`. |

## Design notes

- **Counters and histograms are emit-only, on the `Meter` API directly** (`Counter<T>.Add`,
  `Histogram<T>.Record`) -- no additional locking, dictionary write, or aggregation on the write
  path, so they stay safe on the engine's hottest paths (every evaluation; every champion
  broadcast). This mirrors `CounterRegistry`'s own "emit, don't aggregate on the write side" shape.
- **The three gauges are pull-, not push-based.** A source (a `TowerScheme<TGenome>.ProblemTower`
  or `GenomeFactoryBase<TGenome>` instance) registers a cheap `Func<long>` read callback *once*,
  at construction; the callback only actually runs when a listener polls (an `ObservableGauge<T>`
  is collected at a bounded, low frequency -- typically once per second for `dotnet-counters` or
  an OpenTelemetry `PeriodicExportingMetricReader`). No per-generation or per-genome write ever
  touches `EngineInstruments`.
- **Sources are held weakly** (`ConditionalWeakTable<object, ...>` keyed by the owning tower or
  factory instance): once an owner becomes unreachable its gauge source is dropped automatically
  on the next collection sweep. There is no explicit unregister/dispose call for a caller to
  remember, and registering here can never keep an otherwise-dead tower or factory alive.
- **Multiple concurrent owners are supported.** If more than one `ProblemTower` or
  `GenomeFactoryBase<TGenome>` is alive in the same process (e.g. several problems, or several
  test instances), each contributes its own measurement to the corresponding gauge on every
  collection pass -- readers see one row per live instance, not a single process-wide total.

## Verifying instrument presence

`Solve.Tests` covers instrument presence and recorded-value plumbing via a `MeterListener`
attached to `EngineInstruments.Meter` (or matched by `EngineInstruments.MeterName`) -- see
`Solve.Tests/EngineInstrumentsTests.cs`.

## `dotnet-counters` / OpenTelemetry export

- Live inspection: `dotnet-counters monitor --process-id <pid> --counters Solve.Metrics.Engine`
  against a running benchmark process. See the task's completion notes for a captured excerpt.
- The Eater benchmark harness (`Problems/Eater/Benchmark/Program.cs`) accepts an opt-in exporter
  flag/env var that stands up an OpenTelemetry `MeterProvider` subscribed to
  `Solve.Metrics.Engine` (plus `Solve.Metrics.*` for the `CounterRegistry`-backed counters), using
  the OTLP exporter when a collector endpoint is configured and the console exporter otherwise.
  Exporter package references live only in `Problems/Eater/Benchmark/Eater.Benchmark.csproj` --
  `Solve` itself takes no exporter/OpenTelemetry package references, only the BCL
  `System.Diagnostics.Metrics` types.
