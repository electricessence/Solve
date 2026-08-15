using Solve.ProcessingSchemes;
using Solve.Telemetry;
using System.Text.Json;

namespace Solve.Tests;

public class RunManifestTests
{
	[Fact]
	public void Create_WithNoSchemeConfigOrSeed_ProducesNullsRatherThanFailing()
	{
		// A directory that is guaranteed not to be inside a git working tree.
		string nonRepoDir = CreateNonRepoTempDirectory();
		try
		{
			RunManifest manifest = RunManifestFactory.Create(
				schemeConfig: null,
				seed: null,
				repositoryDirectory: nonRepoDir);

			Assert.Null(manifest.SchemeConfig);
			Assert.Null(manifest.Seed);
			Assert.Null(manifest.GitCommitHash);

			Assert.True(manifest.ProcessorCount > 0);
			Assert.False(string.IsNullOrWhiteSpace(manifest.OSDescription));
			Assert.False(string.IsNullOrWhiteSpace(manifest.RuntimeVersion));
			Assert.False(string.IsNullOrWhiteSpace(manifest.GCMode));
			Assert.True(manifest.StartedUtc <= DateTime.UtcNow);
			Assert.True(manifest.StartedUtc > DateTime.UtcNow.AddMinutes(-5));
		}
		finally
		{
			Directory.Delete(nonRepoDir, recursive: true);
		}
	}

	[Fact]
	public void Create_WithSchemeConfig_CapturesFlattenedSnapshot()
	{
		var config = new SchemeConfig
		{
			MaxLevels = 500,
			MaxLevelLoss = 3,
			MaxConsecutiveRejections = 10,
			PercentRejectedBeforeElimination = 70,
			PoolSize = (400, 40, 2),
		};

		RunManifest manifest = RunManifestFactory.Create(schemeConfig: config, seed: 12345);

		Assert.NotNull(manifest.SchemeConfig);
		Assert.Equal(400, manifest.SchemeConfig!.PoolSizeFirst);
		Assert.Equal(40, manifest.SchemeConfig.PoolSizeMinimum);
		Assert.Equal(2, manifest.SchemeConfig.PoolSizeStep);
		Assert.Equal(500, manifest.SchemeConfig.MaxLevels);
		Assert.Equal(3, manifest.SchemeConfig.MaxLevelLoss);
		Assert.Equal(10, manifest.SchemeConfig.MaxConsecutiveRejections);
		Assert.Equal(70, manifest.SchemeConfig.PercentRejectedBeforeElimination);
		Assert.Equal(12345, manifest.Seed);
	}

	[Fact]
	public void TryGetGitCommitHash_InNonRepoDirectory_ReturnsNull()
	{
		string nonRepoDir = CreateNonRepoTempDirectory();
		try
		{
			string? hash = RunManifestFactory.TryGetGitCommitHash(nonRepoDir, TimeSpan.FromSeconds(2));
			Assert.Null(hash);
		}
		finally
		{
			Directory.Delete(nonRepoDir, recursive: true);
		}
	}

	[Fact]
	public void TryGetGitCommitHash_NonexistentDirectory_ReturnsNullRatherThanThrowing()
	{
		string missingDir = Path.Combine(Path.GetTempPath(), "solve-manifest-test-missing-" + Guid.NewGuid().ToString("N"));
		string? hash = RunManifestFactory.TryGetGitCommitHash(missingDir);
		Assert.Null(hash);
	}

	[Fact]
	public void TryGetGitCommitHash_EmptyWorkingDirectory_ReturnsNullRatherThanThrowing()
	{
		string? hash = RunManifestFactory.TryGetGitCommitHash(string.Empty);
		Assert.Null(hash);
	}

	[Fact]
	public void Write_ProducesSchemaStableJson_WithExactPropertyNames()
	{
		var config = new SchemeConfig
		{
			MaxLevels = 500,
			PoolSize = (400, 40, 2),
		};
		RunManifest manifest = RunManifestFactory.Create(schemeConfig: config, seed: 42, repositoryDirectory: CreateNonRepoTempDirectory());

		string path = Path.Combine(Path.GetTempPath(), "solve-manifest-test-" + Guid.NewGuid().ToString("N") + ".json");
		try
		{
			RunManifestFactory.Write(path, manifest);

			Assert.True(File.Exists(path));
			using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
			JsonElement root = doc.RootElement;

			// The manifest schema is a stable, documented contract (see RunManifest's XML doc
			// remarks): property names ARE the JSON field names, no naming-policy translation.
			Assert.True(root.TryGetProperty("StartedUtc", out _));
			Assert.True(root.TryGetProperty("OSDescription", out _));
			Assert.True(root.TryGetProperty("ProcessorCount", out _));
			Assert.True(root.TryGetProperty("RuntimeVersion", out _));
			Assert.True(root.TryGetProperty("InformationalVersion", out _));
			Assert.True(root.TryGetProperty("GitCommitHash", out _));
			Assert.True(root.TryGetProperty("GCMode", out _));
			Assert.True(root.TryGetProperty("SchemeConfig", out JsonElement schemeConfigElement));
			Assert.True(root.TryGetProperty("Seed", out JsonElement seedElement));

			Assert.Equal(42, seedElement.GetInt64());
			Assert.True(schemeConfigElement.TryGetProperty("PoolSizeFirst", out JsonElement first));
			Assert.Equal(400, first.GetInt32());
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	[Fact]
	public void Write_WithNoSchemeConfigOrSeed_SerializesNullFieldsWithoutThrowing()
	{
		RunManifest manifest = RunManifestFactory.Create(
			schemeConfig: null,
			seed: null,
			repositoryDirectory: CreateNonRepoTempDirectory());

		string path = Path.Combine(Path.GetTempPath(), "solve-manifest-test-" + Guid.NewGuid().ToString("N") + ".json");
		try
		{
			RunManifestFactory.Write(path, manifest);

			using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
			JsonElement root = doc.RootElement;

			Assert.Equal(JsonValueKind.Null, root.GetProperty("SchemeConfig").ValueKind);
			Assert.Equal(JsonValueKind.Null, root.GetProperty("Seed").ValueKind);
			Assert.Equal(JsonValueKind.Null, root.GetProperty("GitCommitHash").ValueKind);
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	private static string CreateNonRepoTempDirectory()
	{
		string dir = Path.Combine(Path.GetTempPath(), "solve-manifest-test-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		return dir;
	}
}
