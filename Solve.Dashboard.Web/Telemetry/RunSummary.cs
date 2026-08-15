/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using System.Text.Json;

namespace Solve.Dashboard.Web.Telemetry;

// These records intentionally mirror the field shapes documented in
// Solve/Telemetry/RunEventLog.schema.md rather than reusing Solve.Telemetry.RunEvent and its
// [JsonDerivedType]s directly -- see the <remarks> on EventFileTailer for why this project has no
// compile-time dependency on the engine assembly. RunSummaryAggregator is the single place that
// translates the wire schema into these types.

/// <summary>One entry of <see cref="StatusSample.TestCounts"/>; mirrors a "status" event's <c>testCounts</c> array entry.</summary>
public sealed record ProblemTestCountSample(int ProblemId, long TestCount);

/// <summary>One entry of <see cref="ChampionSample.FitnessAverages"/>; mirrors a "champion" event's <c>fitnessAverages</c> array entry.</summary>
public sealed record FitnessMetricSample(string Metric, double Value);

/// <summary>Distilled from the run's <c>run_started</c> line, if one has been observed.</summary>
public sealed record RunStartInfo(double Elapsed, DateTime StartedUtc, int ProblemCount, JsonElement? SchemeConfig);

/// <summary>The most recently observed <c>status</c> line.</summary>
public sealed record StatusSample(double Elapsed, IReadOnlyList<ProblemTestCountSample> TestCounts, long ChampionCount);

/// <summary>
/// The most recently observed <c>champion</c> line for one <c>(ProblemId, PoolIndex)</c> pool.
/// Champion broadcasts already only fire when a new champion supersedes the pool's prior one (see
/// RunEventLog.schema.md), so "most recent per pool" and "best per pool" are the same thing here --
/// no independent fitness comparison is needed (or possible in general, since fitness is
/// multi-metric and problem-specific).
/// </summary>
public sealed record ChampionSample(
	double Elapsed,
	int ProblemId,
	int PoolIndex,
	string GenomeHash,
	int GeneCount,
	int SampleCount,
	IReadOnlyList<FitnessMetricSample> FitnessAverages);

/// <summary>Distilled from the run's <c>run_ended</c> line, if one has been observed.</summary>
public sealed record RunEndedSample(double Elapsed, string Reason, long TotalTests);

/// <summary>
/// Aggregate snapshot returned by <c>GET /api/summary</c>; produced by folding a run's JSONL lines
/// through <see cref="RunSummaryAggregator.Aggregate"/>.
/// </summary>
public sealed record RunSummary
{
	public RunStartInfo? RunStart { get; init; }
	public StatusSample? LatestStatus { get; init; }
	public IReadOnlyList<ChampionSample> BestChampions { get; init; } = [];
	public long TotalEventCount { get; init; }
	public RunEndedSample? RunEnded { get; init; }
}
