using Solve.ProcessingSchemes;
using System.Text.Json;

namespace Solve.ExperimentRunner;

/// <summary>
/// Parses and validates experiment definition JSON files (see the project README for the full
/// schema) into <see cref="ExperimentDefinition"/> instances. Every failure mode -- invalid JSON,
/// a missing required field, an unrecognized problem id, or an unrecognized formula name -- raises
/// <see cref="ExperimentDefinitionException"/> with a message that names the offending file and
/// field, so the batch runner can report it and move on to the next definition (see the "malformed
/// definition" acceptance criterion this exists to satisfy).
/// </summary>
public static class ExperimentDefinitionReader
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	/// <summary>Reads and parses a definition file from disk.</summary>
	/// <exception cref="ExperimentDefinitionException">
	/// The file could not be read, or its contents failed validation.
	/// </exception>
	public static ExperimentDefinition Read(string path)
	{
		ArgumentException.ThrowIfNullOrEmpty(path);

		string json;
		try
		{
			json = File.ReadAllText(path);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			throw new ExperimentDefinitionException($"Could not read definition file '{path}': {ex.Message}", ex);
		}

		return Parse(json, path);
	}

	/// <summary>
	/// Parses and validates definition JSON already in memory. <paramref name="sourceLabel"/> is
	/// used only to make exception messages identify the offending file; it does not affect parsing.
	/// </summary>
	/// <exception cref="ExperimentDefinitionException">The content failed validation.</exception>
	public static ExperimentDefinition Parse(string json, string? sourceLabel = null)
	{
		ArgumentNullException.ThrowIfNull(json);
		string label = string.IsNullOrEmpty(sourceLabel) ? "<definition>" : sourceLabel;

		Dto? dto;
		try
		{
			dto = JsonSerializer.Deserialize<Dto>(json, JsonOptions);
		}
		catch (JsonException ex)
		{
			throw new ExperimentDefinitionException($"'{label}' is not valid JSON: {ex.Message}", ex);
		}

		if (dto is null)
			throw new ExperimentDefinitionException($"'{label}' is empty or parses to 'null'.");

		string name = RequireNonEmpty(dto.Name, "name", label);
		string outputDirectory = RequireNonEmpty(dto.OutputDirectory, "outputDirectory", label);

		if (dto.Problem is null)
			throw new ExperimentDefinitionException($"'{label}': 'problem' is required.");

		ExperimentProblemDefinition problem = ParseProblem(dto.Problem, label);
		SchemeConfig.PoolSizing poolSize = ParsePoolSize(dto.PoolSize, label);

		if (dto.MaxLevels is not { } maxLevels || maxLevels == 0)
			throw new ExperimentDefinitionException($"'{label}': 'maxLevels' is required and must be greater than zero.");

		if (dto.DurationMinutes is not { } durationMinutes || durationMinutes <= 0)
			throw new ExperimentDefinitionException($"'{label}': 'durationMinutes' is required and must be greater than zero.");

		TimeSpan? stagnationWindow = null;
		if (dto.StagnationMinutes is { } stagnationMinutes)
		{
			if (stagnationMinutes <= 0)
				throw new ExperimentDefinitionException($"'{label}': 'stagnationMinutes', if present, must be greater than zero.");

			stagnationWindow = TimeSpan.FromMinutes(stagnationMinutes);
		}

		return new ExperimentDefinition(
			name, problem, poolSize, maxLevels, durationMinutes, stagnationWindow, dto.Seed, outputDirectory);
	}

	private static ExperimentProblemDefinition ParseProblem(ProblemDto dto, string label)
	{
		string id = RequireNonEmpty(dto.Id, "problem.id", label);

		return id.ToLowerInvariant() switch
		{
			"eater" => new EaterExperimentProblem(dto.GridSize ?? 10),
			"blackbox" or "blackboxfunction" => ParseBlackBox(dto, label),
			_ => throw new ExperimentDefinitionException(
				$"'{label}': unknown problem id '{dto.Id}'. Expected 'eater' or 'blackbox'."),
		};
	}

	private static BlackBoxExperimentProblem ParseBlackBox(ProblemDto dto, string label)
	{
		string formula = RequireNonEmpty(dto.Formula, "problem.formula", label);
		if (!Formulas.ByName.ContainsKey(formula))
		{
			throw new ExperimentDefinitionException(
				$"'{label}': unknown formula '{formula}'. Available: {string.Join(", ", Formulas.ByName.Keys)}.");
		}

		return new BlackBoxExperimentProblem(formula, dto.SampleSize ?? 200);
	}

	private static SchemeConfig.PoolSizing ParsePoolSize(PoolSizeDto? dto, string label)
	{
		if (dto is null)
			throw new ExperimentDefinitionException($"'{label}': 'poolSize' is required.");

		if (dto.First is not { } first)
			throw new ExperimentDefinitionException($"'{label}': 'poolSize.first' is required.");
		if (dto.Minimum is not { } minimum)
			throw new ExperimentDefinitionException($"'{label}': 'poolSize.minimum' is required.");
		if (dto.Step is not { } step)
			throw new ExperimentDefinitionException($"'{label}': 'poolSize.step' is required.");

		try
		{
			return new SchemeConfig.PoolSizing(first, minimum, step);
		}
		catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
		{
			throw new ExperimentDefinitionException(
				$"'{label}': invalid poolSize ({first}, {minimum}, {step}): {ex.Message}", ex);
		}
	}

	private static string RequireNonEmpty(string? value, string field, string label)
		=> string.IsNullOrWhiteSpace(value)
			? throw new ExperimentDefinitionException($"'{label}': '{field}' is required.")
			: value;

	// Deserialization targets: intentionally all-nullable so a missing field is distinguishable
	// (null) from a present-but-invalid one, which lets the validation above give a specific
	// "field X is required" message rather than a generic deserialization failure.
	private sealed class Dto
	{
		public string? Name { get; set; }
		public ProblemDto? Problem { get; set; }
		public PoolSizeDto? PoolSize { get; set; }
		public ushort? MaxLevels { get; set; }
		public double? DurationMinutes { get; set; }
		public double? StagnationMinutes { get; set; }
		public long? Seed { get; set; }
		public string? OutputDirectory { get; set; }
	}

	private sealed class ProblemDto
	{
		public string? Id { get; set; }

		// Eater
		public ushort? GridSize { get; set; }

		// BlackBoxFunction
		public string? Formula { get; set; }
		public ushort? SampleSize { get; set; }
	}

	private sealed class PoolSizeDto
	{
		public ushort? First { get; set; }
		public ushort? Minimum { get; set; }
		public ushort? Step { get; set; }
	}
}
