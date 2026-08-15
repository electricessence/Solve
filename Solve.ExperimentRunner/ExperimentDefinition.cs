using Solve.ProcessingSchemes;

namespace Solve.ExperimentRunner;

/// <summary>
/// Base type for the problem-specific portion of an <see cref="ExperimentDefinition"/>. Concrete
/// subtypes (<see cref="EaterExperimentProblem"/>, <see cref="BlackBoxExperimentProblem"/>) carry
/// only the fields that problem needs; <see cref="ExperimentDefinitionReader"/> is the sole place
/// that maps a JSON <c>problem.id</c> value onto one of these.
/// </summary>
/// <param name="Id">The problem selector as it appears in the JSON definition (e.g. <c>"eater"</c>).</param>
public abstract record ExperimentProblemDefinition(string Id);

/// <summary>The Eater problem, sized by its square grid.</summary>
public sealed record EaterExperimentProblem(ushort GridSize) : ExperimentProblemDefinition("eater");

/// <summary>The BlackBoxFunction (symbolic regression) problem, selected by a named formula.</summary>
public sealed record BlackBoxExperimentProblem(string Formula, ushort SampleSize) : ExperimentProblemDefinition("blackbox");

/// <summary>
/// A fully parsed and validated experiment definition, ready to execute. Produced only by
/// <see cref="ExperimentDefinitionReader"/> -- every instance in circulation is known-valid,
/// so downstream execution code (<see cref="ExperimentRun"/>) does not need to re-check any of
/// these fields.
/// </summary>
/// <param name="Name">Human-readable identifier for this run; used to name output artifacts and as the results-index record's key.</param>
/// <param name="Problem">Which problem to run and its problem-specific configuration.</param>
/// <param name="PoolSize">Champion-pool sizing triple (first, minimum, step) applied to the tower scheme.</param>
/// <param name="MaxLevels">Maximum tower level the scheme may grow to.</param>
/// <param name="DurationMinutes">Wall-clock time budget for the run; the scheme is cancelled once this elapses (unless it ends earlier on its own).</param>
/// <param name="StagnationWindow">
/// Optional: cancel the run early if no per-pool fitness improvement occurs within this window.
/// <see langword="null"/> disables stagnation-based termination.
/// </param>
/// <param name="Seed">
/// Optional RNG seed. Accepted and recorded for provenance, but NOT currently applied to genome
/// generation: the underlying engine has no seedable-RNG injection point yet (tracked separately).
/// See <see cref="ExperimentRun"/>'s summary output, which reports this explicitly as unsupported.
/// </param>
/// <param name="OutputDirectory">Directory (relative to the current working directory, unless rooted) that per-run artifacts are written into.</param>
public sealed record ExperimentDefinition(
	string Name,
	ExperimentProblemDefinition Problem,
	SchemeConfig.PoolSizing PoolSize,
	ushort MaxLevels,
	double DurationMinutes,
	TimeSpan? StagnationWindow,
	long? Seed,
	string OutputDirectory);

/// <summary>
/// Thrown by <see cref="ExperimentDefinitionReader"/> when a definition file is malformed: invalid
/// JSON, a missing required field, or a problem id/formula name that isn't recognized. The message
/// is intended to be shown to a user directly (see the batch runner's per-file error handling in
/// <c>Program.cs</c>, which catches this type specifically so one bad file cannot abort the batch).
/// </summary>
public sealed class ExperimentDefinitionException : Exception
{
	public ExperimentDefinitionException(string message) : base(message) { }
	public ExperimentDefinitionException(string message, Exception innerException) : base(message, innerException) { }
	public ExperimentDefinitionException() { }
}
