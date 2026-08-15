/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using System.Text.Json;

namespace Solve.Dashboard.Web.Telemetry;

/// <summary>
/// Folds raw JSONL run-event lines (see <c>Solve/Telemetry/RunEventLog.schema.md</c>) into a
/// <see cref="RunSummary"/>.
/// </summary>
/// <remarks>
/// Pure function of its input lines: no file I/O, no state held between calls.
/// <c>GET /api/summary</c> re-aggregates from a fresh <see cref="JsonlFile.ReadCompleteLines"/>
/// snapshot on every request, so there is nothing here that can drift out of sync with the file, and
/// this type is trivially testable against literal fixture strings.
/// </remarks>
public static class RunSummaryAggregator
{
	public static RunSummary Aggregate(IEnumerable<string> jsonLines)
	{
		ArgumentNullException.ThrowIfNull(jsonLines);

		RunStartInfo? runStart = null;
		StatusSample? latestStatus = null;
		var bestChampions = new Dictionary<(int ProblemId, int PoolIndex), ChampionSample>();
		RunEndedSample? runEnded = null;
		long totalEventCount = 0;

		foreach (string line in jsonLines)
		{
			if (string.IsNullOrWhiteSpace(line)) continue;

#pragma warning disable CA1031 // Do not catch general exception types: one malformed or unexpectedly-shaped line must not fail the whole summary -- see RunEventLog.cs's own DrainAsync for the same "telemetry must never be fatal" posture on the writer side.
			try
			{
				using JsonDocument doc = JsonDocument.Parse(line);
				JsonElement root = doc.RootElement;

				if (!root.TryGetProperty("type", out JsonElement typeElement)) continue;
				string? type = typeElement.GetString();
				if (type is null) continue;

				totalEventCount++;
				double elapsed = root.GetProperty("elapsed").GetDouble();

				switch (type)
				{
					case "run_started":
						runStart = new RunStartInfo(
							elapsed,
							root.GetProperty("startedUtc").GetDateTime(),
							root.GetProperty("problemCount").GetInt32(),
							root.TryGetProperty("schemeConfig", out JsonElement schemeConfig) && schemeConfig.ValueKind != JsonValueKind.Null
								? schemeConfig.Clone() // must clone: `doc` (and therefore `root`) is disposed at the end of this iteration.
								: null);
						break;

					case "status":
						List<ProblemTestCountSample> counts = [.. root.GetProperty("testCounts").EnumerateArray()
							.Select(e => new ProblemTestCountSample(e.GetProperty("problemId").GetInt32(), e.GetProperty("testCount").GetInt64()))];
						latestStatus = new StatusSample(elapsed, counts, root.GetProperty("championCount").GetInt64());
						break;

					case "champion":
						int problemId = root.GetProperty("problemId").GetInt32();
						int poolIndex = root.GetProperty("poolIndex").GetInt32();
						List<FitnessMetricSample> averages = [.. root.GetProperty("fitnessAverages").EnumerateArray()
							.Select(e => new FitnessMetricSample(e.GetProperty("metric").GetString() ?? "", e.GetProperty("value").GetDouble()))];
						bestChampions[(problemId, poolIndex)] = new ChampionSample(
							elapsed, problemId, poolIndex,
							root.GetProperty("genomeHash").GetString() ?? "",
							root.GetProperty("geneCount").GetInt32(),
							root.GetProperty("sampleCount").GetInt32(),
							averages);
						break;

					case "run_ended":
						runEnded = new RunEndedSample(
							elapsed,
							root.GetProperty("reason").GetString() ?? "",
							root.GetProperty("totalTests").GetInt64());
						break;

						// "level_created" and "fault" contribute to TotalEventCount only -- neither
						// has a dedicated RunSummary field per acceptance criterion 5. An unrecognized
						// future "type" behaves the same way: counted, otherwise ignored.
				}
			}
			catch (Exception)
			{
				// Malformed JSON, or a syntactically-valid line missing/mistyping a field its type
				// requires -- skip it and keep folding the rest of the file.
			}
#pragma warning restore CA1031
		}

		return new RunSummary
		{
			RunStart = runStart,
			LatestStatus = latestStatus,
			BestChampions = [.. bestChampions.Values.OrderBy(c => c.ProblemId).ThenBy(c => c.PoolIndex)],
			TotalEventCount = totalEventCount,
			RunEnded = runEnded,
		};
	}
}
