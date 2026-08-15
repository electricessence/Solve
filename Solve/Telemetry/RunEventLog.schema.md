# RunEventLog JSONL schema

`RunEventLog<TGenome>` (`Solve/Telemetry/RunEventLog.cs`) appends one JSON object per line to a
caller-supplied file. Each line is independently parseable (JSON Lines / [JSONL](https://jsonlines.org/)):
no wrapping array, no trailing comma, `\n`-terminated. This document is the human-readable view of
that schema; the C# records in `RunEventLog.cs` (`RunEvent` and its `[JsonDerivedType]`s) are the
source of truth -- `[JsonPropertyName]` on every field pins the wire name so a future rename of a
C# property does not silently change the contract described here.

## Common envelope

Every line has at least:

| Field     | Type   | Description                                                            |
|-----------|--------|--------------------------------------------------------------------------|
| `type`    | string | Discriminator; one of the event names below.                           |
| `elapsed` | number | Seconds (fractional) since the `RunEventLog` instance was constructed. |

Field names elsewhere in this document are camelCase, matching the wire format. `type` values are
snake_case, matching the acceptance criteria's event-name convention.

## Event types

### `run_started`

Written once, synchronously inside `Attach(...)`, before any other event -- so it is always the
first line (assuming a single `RunEventLog` instance per file, which is the intended usage).

| Field          | Type              | Description                                                                 |
|----------------|-------------------|-------------------------------------------------------------------------------|
| `startedUtc`   | string (ISO 8601) | `DateTime.UtcNow` at attach time.                                            |
| `schemeConfig` | object \| null    | `SchemeConfigSnapshot` (from `RunManifest.cs`, reused here) -- present when a scheme config was supplied to `Attach`, `null` otherwise. |
| `problemCount` | integer           | `problems.Count` at attach time.                                            |

```json
{"type":"run_started","elapsed":0.0006,"startedUtc":"2026-08-15T18:04:12.5031Z","schemeConfig":{"PoolSizeFirst":400,"PoolSizeMinimum":40,"PoolSizeStep":2,"MaxLevels":500,"MaxLevelLoss":3,"MaxConsecutiveRejections":10,"PercentRejectedBeforeElimination":70},"problemCount":1}
```

### `level_created`

One per `TowerScheme<TGenome>.LevelCreated` notification. Only emitted when the attached
environment is a `TowerScheme<TGenome>` (the only scheme that currently exposes this stream, per
task 15-0018); otherwise this event type never appears in the file.

| Field       | Type    | Description                          |
|-------------|---------|---------------------------------------|
| `problemId` | integer | `IProblem<TGenome>.ID` the level belongs to. |
| `level`     | integer | The level index that was created.     |

```json
{"type":"level_created","elapsed":12.401,"problemId":1,"level":7}
```

### `champion`

One per champion broadcast from the scheme's `EnvironmentBase<TGenome>`.

| Field             | Type    | Description                                              |
|-------------------|---------|------------------------------------------------------------|
| `problemId`       | integer | `IProblem<TGenome>.ID`.                                  |
| `poolIndex`       | integer | Pool index the champion belongs to.                       |
| `genomeHash`      | string  | `TGenome.Hash`.                                           |
| `geneCount`       | integer | `TGenome.GeneCount`.                                      |
| `sampleCount`     | integer | `Fitness.SampleCount` at broadcast time.                   |
| `fitnessAverages` | array   | One `{"metric": string, "value": number}` per `Fitness.Metrics` entry, in metric order, from `Fitness.MetricAverages`. |

```json
{"type":"champion","elapsed":45.2,"problemId":1,"poolIndex":0,"genomeHash":"ab12cd34","geneCount":19,"sampleCount":312,"fitnessAverages":[{"metric":"FoodFoundRate","value":0.87},{"metric":"Steps","value":142.5}]}
```

### `status`

Emitted periodically, once per `samplingInterval` passed to the constructor, for as long as the
log is attached.

| Field          | Type    | Description                                                          |
|----------------|---------|--------------------------------------------------------------------|
| `testCounts`   | array   | One `{"problemId": integer, "testCount": integer}` per attached problem, in the order `EnvironmentBase<TGenome>.Problems` enumerates them. |
| `championCount`| integer | Total `champion` events observed so far (cumulative, this run).      |

```json
{"type":"status","elapsed":60.0,"testCounts":[{"problemId":1,"testCount":48213}],"championCount":37}
```

### `fault`

Written whenever a subscribed source (the champion broadcast or the level-created stream) calls
`OnError`, or when processing an event throws. Never crashes the run that produced it -- see the
`catch (Exception)` blocks in `RunEventLog.cs`.

| Field     | Type           | Description                                                        |
|-----------|----------------|----------------------------------------------------------------------|
| `message` | string         | `Exception.GetBaseException().Message`.                              |
| `source`  | string \| null | Which subscription faulted: `"championBroadcast"`, `"levelCreated"`, or `"status"`. |

```json
{"type":"fault","elapsed":88.9,"message":"Evaluation queue faulted.","source":"championBroadcast"}
```

### `run_ended`

Written exactly once per `RunEventLog` instance. Whichever of the following happens first wins
(subsequent triggers are no-ops -- see `TryWriteRunEnded`):

- the champion broadcast completes (`reason`: `"Completed."`)
- the champion broadcast faults (`reason`: `"Faulted: {message}"`)
- the cancellation token passed to `Attach` is cancelled (`reason`: `"Cancelled."`)
- `RunEventLog.Dispose()` runs and none of the above already fired (`reason`: `"Disposed."`)

| Field        | Type    | Description                                                    |
|--------------|---------|--------------------------------------------------------------------|
| `reason`     | string  | One of the reasons above.                                       |
| `totalTests` | integer | Sum of `IProblem<TGenome>.TestCount` across all attached problems at the moment `run_ended` was written. |

`elapsed` on this event is the total run duration as observed by the logger.

```json
{"type":"run_ended","elapsed":1200.7,"reason":"Disposed.","totalTests":612044}
```

## Writer design

Writes are non-blocking for the engine hot path: every method reachable from a broadcast
(`OnChampion`, `OnLevelCreated`) only enqueues into an in-memory, unbounded
`System.Threading.Channels.Channel<RunEvent>` (`ChannelWriter<T>.TryWrite`, O(1), no I/O). A single
dedicated background `Task` drains that channel (`ChannelReader<T>.ReadAllAsync`), serializes each
event with `System.Text.Json`, and writes it as one line to a buffered `StreamWriter`, flushing
after every line so a concurrent tailer (the live-dashboard host this component exists to feed --
see epic 15-0032) sees events promptly. `Dispose()` completes the channel, synchronously waits for
the drain task, then flushes and closes the writer -- so every event enqueued before `Dispose` is
guaranteed to be on disk when it returns.

See the `<remarks>` on `RunEventLog<TGenome>` in `RunEventLog.cs` for the full rationale (including
why a channel-drained single writer was chosen over a locked/buffered `StreamWriter` written to
directly from callers).
