# Solve.Dashboard.Web

Sidecar ASP.NET Core host that tails a `RunEventLog` JSONL file (see
`Solve/Telemetry/RunEventLog.schema.md`) and serves it to any browser client over Server-Sent
Events, plus a JSON aggregate snapshot. It only reads the file -- it never talks to the run process
that wrote it, so it works identically for live runs, crashed runs, and archived runs, and cannot
destabilize an experiment.

## Launch

	dotnet run --project Solve.Dashboard.Web -- <path-to-events.jsonl> [--port NNNN]

- `<path-to-events.jsonl>` (required, positional) -- path to the JSONL file to tail. Need not exist
  yet; the host starts polling and picks up lines once the file appears.
- `--port NNNN` (optional) -- port to listen on. **Default: 5299.**

## Endpoints

- `GET /events` -- Server-Sent Events (`text/event-stream`). Replays every line currently in the
  file, in file order, as `data: <raw JSON line>` frames, then keeps the connection open and streams
  each new line as it is appended (polling roughly every 200ms). A trailing line still being written
  is held back until its terminating newline arrives -- it is never surfaced as an error mid-stream,
  and never ends the connection.
- `GET /api/summary` -- current aggregate snapshot as JSON: run-start info, the latest `status`
  sample, the best (most recently broadcast) champion per `(problemId, poolIndex)` pool, the total
  event count, and `run_ended` details once the run has ended. Recomputed from the file on every
  request, so it is always consistent with what `/events` has replayed up to that point.
- `GET /` -- static placeholder page (the real dashboard UI is task 15-0034).

## Design notes

The tail (`Telemetry/EventFileTailer.cs`) and aggregation (`Telemetry/RunSummaryAggregator.cs`)
logic are plain classes with no ASP.NET Core dependency, and deliberately independent of
`Solve.Telemetry.RunEvent` and the engine assembly -- this project only needs to understand the
JSONL wire format documented in `RunEventLog.schema.md`, not the writer's C# types. See
`Solve.Tests/DashboardEventFileTailerTests.cs` and `Solve.Tests/DashboardRunSummaryAggregatorTests.cs`
for their test coverage.
