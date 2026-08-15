namespace Solve.ExperimentRunner;

/// <summary>Best-observed result for a single champion pool at the end of a run.</summary>
public sealed record PoolResultRecord(
	int Pool,
	string? Hash,
	int GeneCount,
	int SampleCount,
	IReadOnlyDictionary<string, double> Metrics);

/// <summary>
/// Full end-of-run artifact written as <c>&lt;name&gt;.summary.json</c> alongside a run's champion
/// CSV. A superset of <see cref="ExperimentIndexRecord"/> -- the index line is a compact pointer
/// back to this file plus the handful of fields useful for at-a-glance batch comparison.
/// </summary>
public sealed record ExperimentSummary(
	string Name,
	string DefinitionFile,
	DateTime StartedUtc,
	DateTime CompletedUtc,
	TimeSpan Elapsed,
	long TotalTests,
	long ChampionBroadcasts,
	string TerminationReason,
	long? Seed,
	bool SeedSupported,
	IReadOnlyList<PoolResultRecord> Pools);

/// <summary>
/// One line of <c>results-index.jsonl</c>: enough to compare completed (and failed) runs across a
/// whole batch without opening each run's individual summary file.
/// </summary>
public sealed record ExperimentIndexRecord(
	string Name,
	string DefinitionFile,
	double DurationSeconds,
	long TotalTests,
	string TerminationReason,
	IReadOnlyList<PoolResultRecord>? Pools,
	string? Error)
{
	/// <summary>Builds the record written for a definition file that could not be parsed or run.</summary>
	public static ExperimentIndexRecord Failed(string name, string definitionFile, string error)
		=> new(name, definitionFile, DurationSeconds: 0, TotalTests: 0, TerminationReason: "Failed", Pools: null, Error: error);
}
