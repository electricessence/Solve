using Solve.ExperimentRunner;

namespace Solve.Tests;

public class ExperimentDefinitionReaderTests
{
	private const string ValidEaterJson = """
		{
			"name": "eater-smoke",
			"problem": { "id": "eater", "gridSize": 6 },
			"poolSize": { "first": 100, "minimum": 20, "step": 2 },
			"maxLevels": 40,
			"durationMinutes": 1,
			"outputDirectory": "out/eater-smoke"
		}
		""";

	private const string ValidBlackBoxJson = """
		{
			"name": "blackbox-smoke",
			"problem": { "id": "blackbox", "formula": "AB", "sampleSize": 50 },
			"poolSize": { "first": 100, "minimum": 20, "step": 2 },
			"maxLevels": 40,
			"durationMinutes": 1,
			"stagnationMinutes": 5,
			"seed": 12345,
			"outputDirectory": "out/blackbox-smoke"
		}
		""";

	[Fact]
	public void Parse_ValidEaterDefinition_ProducesExpectedValues()
	{
		ExperimentDefinition definition = ExperimentDefinitionReader.Parse(ValidEaterJson);

		Assert.Equal("eater-smoke", definition.Name);
		EaterExperimentProblem problem = Assert.IsType<EaterExperimentProblem>(definition.Problem);
		Assert.Equal("eater", problem.Id);
		Assert.Equal((ushort)6, problem.GridSize);
		Assert.Equal((ushort)100, definition.PoolSize.First);
		Assert.Equal((ushort)20, definition.PoolSize.Minimum);
		Assert.Equal((ushort)2, definition.PoolSize.Step);
		Assert.Equal((ushort)40, definition.MaxLevels);
		Assert.Equal(1, definition.DurationMinutes);
		Assert.Null(definition.StagnationWindow);
		Assert.Null(definition.Seed);
		Assert.Equal("out/eater-smoke", definition.OutputDirectory);
	}

	[Fact]
	public void Parse_ValidBlackBoxDefinition_ProducesExpectedValues()
	{
		ExperimentDefinition definition = ExperimentDefinitionReader.Parse(ValidBlackBoxJson);

		Assert.Equal("blackbox-smoke", definition.Name);
		BlackBoxExperimentProblem problem = Assert.IsType<BlackBoxExperimentProblem>(definition.Problem);
		Assert.Equal("blackbox", problem.Id);
		Assert.Equal("AB", problem.Formula);
		Assert.Equal((ushort)50, problem.SampleSize);
		Assert.Equal(TimeSpan.FromMinutes(5), definition.StagnationWindow);
		Assert.Equal(12345, definition.Seed);
	}

	[Fact]
	public void Parse_EaterWithoutGridSize_DefaultsToTen()
	{
		const string json = """
			{
				"name": "eater-default-grid",
				"problem": { "id": "eater" },
				"poolSize": { "first": 100, "minimum": 20, "step": 2 },
				"maxLevels": 40,
				"durationMinutes": 1,
				"outputDirectory": "out"
			}
			""";

		ExperimentDefinition definition = ExperimentDefinitionReader.Parse(json);
		EaterExperimentProblem problem = Assert.IsType<EaterExperimentProblem>(definition.Problem);
		Assert.Equal((ushort)10, problem.GridSize);
	}

	[Fact]
	public void Parse_BlackBoxWithoutSampleSize_DefaultsTo200()
	{
		const string json = """
			{
				"name": "blackbox-default-sample-size",
				"problem": { "id": "blackbox", "formula": "AB" },
				"poolSize": { "first": 100, "minimum": 20, "step": 2 },
				"maxLevels": 40,
				"durationMinutes": 1,
				"outputDirectory": "out"
			}
			""";

		ExperimentDefinition definition = ExperimentDefinitionReader.Parse(json);
		BlackBoxExperimentProblem problem = Assert.IsType<BlackBoxExperimentProblem>(definition.Problem);
		Assert.Equal((ushort)200, problem.SampleSize);
	}

	[Fact]
	public void Parse_InvalidJson_ThrowsWithClearMessage()
	{
		ExperimentDefinitionException ex = Assert.Throws<ExperimentDefinitionException>(
			() => ExperimentDefinitionReader.Parse("{ not valid json", "bad.json"));

		Assert.Contains("bad.json", ex.Message, StringComparison.Ordinal);
		Assert.Contains("not valid JSON", ex.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Parse_EmptyOrNullJson_Throws()
	{
		Assert.Throws<ExperimentDefinitionException>(() => ExperimentDefinitionReader.Parse("null"));
	}

	[Theory]
	[InlineData("name")]
	[InlineData("problem")]
	[InlineData("poolSize")]
	[InlineData("maxLevels")]
	[InlineData("durationMinutes")]
	[InlineData("outputDirectory")]
	public void Parse_MissingRequiredTopLevelField_ThrowsClearError(string fieldToRemove)
	{
		Dictionary<string, object?> fields = new()
		{
			["name"] = "eater-smoke",
			["problem"] = new Dictionary<string, object?> { ["id"] = "eater", ["gridSize"] = 6 },
			["poolSize"] = new Dictionary<string, object?> { ["first"] = 100, ["minimum"] = 20, ["step"] = 2 },
			["maxLevels"] = 40,
			["durationMinutes"] = 1,
			["outputDirectory"] = "out/eater-smoke",
		};
		fields.Remove(fieldToRemove);

		string json = System.Text.Json.JsonSerializer.Serialize(fields);

		ExperimentDefinitionException ex = Assert.Throws<ExperimentDefinitionException>(
			() => ExperimentDefinitionReader.Parse(json, "missing-" + fieldToRemove + ".json"));

		Assert.Contains("missing-" + fieldToRemove + ".json", ex.Message, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("first")]
	[InlineData("minimum")]
	[InlineData("step")]
	public void Parse_MissingPoolSizeField_ThrowsClearError(string fieldToRemove)
	{
		Dictionary<string, object?> poolSize = new()
		{
			["first"] = 100,
			["minimum"] = 20,
			["step"] = 2,
		};
		poolSize.Remove(fieldToRemove);

		var fields = new Dictionary<string, object?>
		{
			["name"] = "eater-smoke",
			["problem"] = new Dictionary<string, object?> { ["id"] = "eater" },
			["poolSize"] = poolSize,
			["maxLevels"] = 40,
			["durationMinutes"] = 1,
			["outputDirectory"] = "out",
		};

		string json = System.Text.Json.JsonSerializer.Serialize(fields);

		ExperimentDefinitionException ex = Assert.Throws<ExperimentDefinitionException>(
			() => ExperimentDefinitionReader.Parse(json));

		Assert.Contains("poolSize." + fieldToRemove, ex.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Parse_UnknownProblemId_ThrowsClearError()
	{
		const string json = """
			{
				"name": "mystery",
				"problem": { "id": "not-a-real-problem" },
				"poolSize": { "first": 100, "minimum": 20, "step": 2 },
				"maxLevels": 40,
				"durationMinutes": 1,
				"outputDirectory": "out"
			}
			""";

		ExperimentDefinitionException ex = Assert.Throws<ExperimentDefinitionException>(
			() => ExperimentDefinitionReader.Parse(json));

		Assert.Contains("unknown problem id", ex.Message, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("not-a-real-problem", ex.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Parse_BlackBoxMissingFormula_ThrowsClearError()
	{
		const string json = """
			{
				"name": "blackbox-no-formula",
				"problem": { "id": "blackbox" },
				"poolSize": { "first": 100, "minimum": 20, "step": 2 },
				"maxLevels": 40,
				"durationMinutes": 1,
				"outputDirectory": "out"
			}
			""";

		ExperimentDefinitionException ex = Assert.Throws<ExperimentDefinitionException>(
			() => ExperimentDefinitionReader.Parse(json));

		Assert.Contains("problem.formula", ex.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Parse_BlackBoxUnknownFormula_ThrowsClearError()
	{
		const string json = """
			{
				"name": "blackbox-bad-formula",
				"problem": { "id": "blackbox", "formula": "NotARealFormula" },
				"poolSize": { "first": 100, "minimum": 20, "step": 2 },
				"maxLevels": 40,
				"durationMinutes": 1,
				"outputDirectory": "out"
			}
			""";

		ExperimentDefinitionException ex = Assert.Throws<ExperimentDefinitionException>(
			() => ExperimentDefinitionReader.Parse(json));

		Assert.Contains("unknown formula", ex.Message, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("NotARealFormula", ex.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Parse_InvalidPoolSizing_ThrowsClearError()
	{
		// Odd numbers violate SchemeConfig.PoolSizing's "must be a multiple of 2" rule.
		const string json = """
			{
				"name": "odd-pool",
				"problem": { "id": "eater" },
				"poolSize": { "first": 101, "minimum": 20, "step": 2 },
				"maxLevels": 40,
				"durationMinutes": 1,
				"outputDirectory": "out"
			}
			""";

		Assert.Throws<ExperimentDefinitionException>(() => ExperimentDefinitionReader.Parse(json));
	}

	[Fact]
	public void Parse_ZeroDurationMinutes_ThrowsClearError()
	{
		const string json = """
			{
				"name": "zero-duration",
				"problem": { "id": "eater" },
				"poolSize": { "first": 100, "minimum": 20, "step": 2 },
				"maxLevels": 40,
				"durationMinutes": 0,
				"outputDirectory": "out"
			}
			""";

		ExperimentDefinitionException ex = Assert.Throws<ExperimentDefinitionException>(
			() => ExperimentDefinitionReader.Parse(json));

		Assert.Contains("durationMinutes", ex.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Parse_NegativeStagnationMinutes_ThrowsClearError()
	{
		const string json = """
			{
				"name": "bad-stagnation",
				"problem": { "id": "eater" },
				"poolSize": { "first": 100, "minimum": 20, "step": 2 },
				"maxLevels": 40,
				"durationMinutes": 1,
				"stagnationMinutes": -5,
				"outputDirectory": "out"
			}
			""";

		ExperimentDefinitionException ex = Assert.Throws<ExperimentDefinitionException>(
			() => ExperimentDefinitionReader.Parse(json));

		Assert.Contains("stagnationMinutes", ex.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Read_FromFile_ParsesSuccessfully()
	{
		string path = Path.Combine(Path.GetTempPath(), "solve-experiment-def-test-" + Guid.NewGuid().ToString("N") + ".json");
		try
		{
			File.WriteAllText(path, ValidEaterJson);
			ExperimentDefinition definition = ExperimentDefinitionReader.Read(path);
			Assert.Equal("eater-smoke", definition.Name);
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	[Fact]
	public void Read_NonexistentFile_ThrowsExperimentDefinitionException()
	{
		string missingPath = Path.Combine(Path.GetTempPath(), "solve-experiment-def-missing-" + Guid.NewGuid().ToString("N") + ".json");
		Assert.Throws<ExperimentDefinitionException>(() => ExperimentDefinitionReader.Read(missingPath));
	}
}
