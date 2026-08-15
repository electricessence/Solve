using Solve.Dashboard.Web.Telemetry;
using System.Text.Json;

namespace Solve.Tests;

/// <summary>
/// Coverage for 15-0033's aggregation logic (<see cref="RunSummaryAggregator"/>): folding raw JSONL
/// lines (see <c>Solve/Telemetry/RunEventLog.schema.md</c>) into the snapshot <c>GET /api/summary</c>
/// returns, per acceptance criterion 5. Fixture lines below are taken verbatim from the schema doc's
/// own one-example-per-type table.
/// </summary>
public class DashboardRunSummaryAggregatorTests
{
	private const string RunStartedLine =
		"{\"type\":\"run_started\",\"elapsed\":0.0006,\"startedUtc\":\"2026-08-15T18:04:12.5031Z\",\"schemeConfig\":{\"PoolSizeFirst\":400,\"PoolSizeMinimum\":40,\"PoolSizeStep\":2,\"MaxLevels\":500,\"MaxLevelLoss\":3,\"MaxConsecutiveRejections\":10,\"PercentRejectedBeforeElimination\":70},\"problemCount\":1}";

	private const string LevelCreatedLine =
		"{\"type\":\"level_created\",\"elapsed\":12.401,\"problemId\":1,\"level\":7}";

	private const string ChampionLine =
		"{\"type\":\"champion\",\"elapsed\":45.2,\"problemId\":1,\"poolIndex\":0,\"genomeHash\":\"ab12cd34\",\"geneCount\":19,\"sampleCount\":312,\"fitnessAverages\":[{\"metric\":\"FoodFoundRate\",\"value\":0.87},{\"metric\":\"Steps\",\"value\":142.5}]}";

	private const string StatusLine =
		"{\"type\":\"status\",\"elapsed\":60.0,\"testCounts\":[{\"problemId\":1,\"testCount\":48213}],\"championCount\":37}";

	private const string FaultLine =
		"{\"type\":\"fault\",\"elapsed\":88.9,\"message\":\"Evaluation queue faulted.\",\"source\":\"championBroadcast\"}";

	private const string RunEndedLine =
		"{\"type\":\"run_ended\",\"elapsed\":1200.7,\"reason\":\"Disposed.\",\"totalTests\":612044}";

	[Fact]
	public void EmptySequenceProducesEmptySummary()
	{
		RunSummary summary = RunSummaryAggregator.Aggregate([]);

		Assert.Null(summary.RunStart);
		Assert.Null(summary.LatestStatus);
		Assert.Empty(summary.BestChampions);
		Assert.Equal(0, summary.TotalEventCount);
		Assert.Null(summary.RunEnded);
	}

	[Fact]
	public void AggregatesAllSixEventTypesFromTheSchemaDocsExamples()
	{
		string[] lines = [RunStartedLine, LevelCreatedLine, ChampionLine, StatusLine, FaultLine, RunEndedLine];

		RunSummary summary = RunSummaryAggregator.Aggregate(lines);

		Assert.Equal(6, summary.TotalEventCount);

		Assert.NotNull(summary.RunStart);
		Assert.Equal(1, summary.RunStart!.ProblemCount);
		using JsonDocument expectedStart = JsonDocument.Parse(RunStartedLine);
		Assert.Equal(expectedStart.RootElement.GetProperty("startedUtc").GetDateTime(), summary.RunStart.StartedUtc);
		Assert.NotNull(summary.RunStart.SchemeConfig);
		Assert.Equal(500, summary.RunStart.SchemeConfig!.Value.GetProperty("MaxLevels").GetInt32());

		Assert.NotNull(summary.LatestStatus);
		Assert.Equal(37, summary.LatestStatus!.ChampionCount);
		Assert.Single(summary.LatestStatus.TestCounts);
		Assert.Equal(1, summary.LatestStatus.TestCounts[0].ProblemId);
		Assert.Equal(48213, summary.LatestStatus.TestCounts[0].TestCount);

		ChampionSample champion = Assert.Single(summary.BestChampions);
		Assert.Equal(1, champion.ProblemId);
		Assert.Equal(0, champion.PoolIndex);
		Assert.Equal("ab12cd34", champion.GenomeHash);
		Assert.Equal(19, champion.GeneCount);
		Assert.Equal(312, champion.SampleCount);
		Assert.Equal(2, champion.FitnessAverages.Count);
		Assert.Equal("FoodFoundRate", champion.FitnessAverages[0].Metric);
		Assert.Equal(0.87, champion.FitnessAverages[0].Value);
		Assert.Equal("Steps", champion.FitnessAverages[1].Metric);
		Assert.Equal(142.5, champion.FitnessAverages[1].Value);

		Assert.NotNull(summary.RunEnded);
		Assert.Equal("Disposed.", summary.RunEnded!.Reason);
		Assert.Equal(612044, summary.RunEnded.TotalTests);
	}

	[Fact]
	public void BestChampionPerPoolTracksTheMostRecentBroadcastForThatPoolOnly()
	{
		const string sameProblemSamePoolNewer =
			"{\"type\":\"champion\",\"elapsed\":50.0,\"problemId\":1,\"poolIndex\":0,\"genomeHash\":\"newer\",\"geneCount\":21,\"sampleCount\":400,\"fitnessAverages\":[]}";
		const string differentProblem =
			"{\"type\":\"champion\",\"elapsed\":46.0,\"problemId\":2,\"poolIndex\":0,\"genomeHash\":\"other-problem\",\"geneCount\":5,\"sampleCount\":10,\"fitnessAverages\":[]}";
		const string samePoolDifferentIndex =
			"{\"type\":\"champion\",\"elapsed\":47.0,\"problemId\":1,\"poolIndex\":1,\"genomeHash\":\"other-pool\",\"geneCount\":8,\"sampleCount\":20,\"fitnessAverages\":[]}";

		RunSummary summary = RunSummaryAggregator.Aggregate(
			[ChampionLine, sameProblemSamePoolNewer, differentProblem, samePoolDifferentIndex]);

		// One entry per distinct (problemId, poolIndex): (1,0), (2,0), (1,1).
		Assert.Equal(3, summary.BestChampions.Count);

		ChampionSample problem1Pool0 = Assert.Single(summary.BestChampions, c => c.ProblemId == 1 && c.PoolIndex == 0);
		Assert.Equal("newer", problem1Pool0.GenomeHash); // superseded "ab12cd34" for the same pool.

		ChampionSample problem2Pool0 = Assert.Single(summary.BestChampions, c => c.ProblemId == 2 && c.PoolIndex == 0);
		Assert.Equal("other-problem", problem2Pool0.GenomeHash);

		ChampionSample problem1Pool1 = Assert.Single(summary.BestChampions, c => c.ProblemId == 1 && c.PoolIndex == 1);
		Assert.Equal("other-pool", problem1Pool1.GenomeHash);
	}

	[Fact]
	public void MalformedJsonLineIsSkippedWithoutAffectingOthers()
	{
		string[] lines = [RunStartedLine, "not json at all {{{", ChampionLine];

		RunSummary summary = RunSummaryAggregator.Aggregate(lines);

		Assert.Equal(2, summary.TotalEventCount); // the garbage line contributes nothing.
		Assert.NotNull(summary.RunStart);
		Assert.Single(summary.BestChampions);
	}

	[Fact]
	public void LineMissingARequiredFieldForItsTypeIsSkippedGracefullyRatherThanThrowing()
	{
		const string incompleteChampion = "{\"type\":\"champion\",\"elapsed\":1.0}"; // missing genomeHash, geneCount, etc.
		string[] lines = [incompleteChampion, ChampionLine];

		RunSummary summary = RunSummaryAggregator.Aggregate(lines);

		// Both lines had a recognizable "type" (so both count), but only the well-formed one
		// produced a champion sample -- the incomplete one must not throw and must not appear.
		Assert.Equal(2, summary.TotalEventCount);
		Assert.Single(summary.BestChampions);
		Assert.Equal("ab12cd34", summary.BestChampions[0].GenomeHash);
	}

	[Fact]
	public void UnknownEventTypeCountsTowardTotalButUpdatesNoDedicatedField()
	{
		const string futureEvent = "{\"type\":\"some_future_event\",\"elapsed\":3.0,\"whatever\":true}";

		RunSummary summary = RunSummaryAggregator.Aggregate([RunStartedLine, futureEvent]);

		Assert.Equal(2, summary.TotalEventCount);
		Assert.NotNull(summary.RunStart);
		Assert.Null(summary.LatestStatus);
		Assert.Empty(summary.BestChampions);
		Assert.Null(summary.RunEnded);
	}

	[Fact]
	public void BlankLinesAreIgnored()
	{
		RunSummary summary = RunSummaryAggregator.Aggregate([RunStartedLine, "", "   ", RunEndedLine]);

		Assert.Equal(2, summary.TotalEventCount);
		Assert.NotNull(summary.RunStart);
		Assert.NotNull(summary.RunEnded);
	}

	[Fact]
	public void LevelCreatedAndFaultLinesCountTowardTotalOnly()
	{
		RunSummary summary = RunSummaryAggregator.Aggregate([LevelCreatedLine, FaultLine]);

		Assert.Equal(2, summary.TotalEventCount);
		Assert.Null(summary.RunStart);
		Assert.Null(summary.LatestStatus);
		Assert.Empty(summary.BestChampions);
		Assert.Null(summary.RunEnded);
	}
}
