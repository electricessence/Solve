using Solve.ProcessingSchemes;
using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Solve.Telemetry;

/// <summary>
/// Provenance captured at the start of a run: what code produced it, on what machine, under
/// what configuration, and (optionally) with what seed. Written as <c>manifest.json</c> beside
/// a run's other output so results can be traced back to their exact run conditions.
/// </summary>
/// <remarks>
/// Serialized as-is via <see cref="JsonSerializer"/> with no naming policy applied, so each
/// property name below IS the JSON field name (e.g. <see cref="StartedUtc"/> serializes as
/// <c>"StartedUtc"</c>). This is the manifest's stable schema: renaming, reordering, or
/// retyping a property here is a breaking change to that schema and should be made
/// deliberately, not as an incidental refactor.
/// </remarks>
public sealed record RunManifest(
	DateTime StartedUtc,
	string OSDescription,
	int ProcessorCount,
	string RuntimeVersion,
	string? InformationalVersion,
	string? GitCommitHash,
	string GCMode,
	SchemeConfigSnapshot? SchemeConfig,
	long? Seed);

/// <summary>
/// Flattened, JSON-friendly snapshot of the <see cref="ISchemeConfig"/> values relevant to
/// reproducing a run.
/// </summary>
/// <remarks>
/// Captured field-by-field rather than serializing an <see cref="ISchemeConfig"/> (or its
/// <see cref="SchemeConfig.Values"/> struct) directly: that struct exposes a self-referential
/// <c>Immutable</c> property (returning itself) which reflection-based JSON serialization would
/// recurse into without end.
/// </remarks>
public sealed record SchemeConfigSnapshot(
	ushort PoolSizeFirst,
	ushort PoolSizeMinimum,
	ushort PoolSizeStep,
	ushort MaxLevels,
	ushort MaxLevelLoss,
	ushort MaxConsecutiveRejections,
	ushort PercentRejectedBeforeElimination)
{
	public static SchemeConfigSnapshot From(ISchemeConfig config)
	{
		ArgumentNullException.ThrowIfNull(config);
		(ushort first, ushort minimum, ushort step) = config.PoolSize;
		return new SchemeConfigSnapshot(
			first, minimum, step,
			config.MaxLevels,
			config.MaxLevelLoss,
			config.MaxConsecutiveRejections,
			config.PercentRejectedBeforeElimination);
	}
}

/// <summary>
/// Static utility that captures a <see cref="RunManifest"/> for the current process and
/// serializes it to disk.
/// </summary>
public static class RunManifestFactory
{
	/// <summary>
	/// Default timeout for the <c>git rev-parse HEAD</c> discovery shell-out.
	/// </summary>
	public static readonly TimeSpan DefaultGitTimeout = TimeSpan.FromSeconds(2);

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
	};

	/// <summary>
	/// Captures run provenance for the current process.
	/// </summary>
	/// <param name="schemeConfig">The active scheme configuration, if any; recorded as a flattened snapshot.</param>
	/// <param name="seed">The RNG seed used for this run, if any; <see langword="null"/> when the run is unseeded.</param>
	/// <param name="repositoryDirectory">
	/// Directory to run <c>git rev-parse HEAD</c> from when discovering the commit hash.
	/// Defaults to <see cref="Environment.CurrentDirectory"/>. When this directory (or any of
	/// its ancestors) is not part of a git working tree — or git is unavailable, or discovery
	/// times out — <see cref="RunManifest.GitCommitHash"/> is <see langword="null"/> rather than
	/// causing a failure.
	/// </param>
	/// <param name="gitTimeout">Timeout for git commit discovery; defaults to <see cref="DefaultGitTimeout"/>.</param>
	public static RunManifest Create(
		ISchemeConfig? schemeConfig = null,
		long? seed = null,
		string? repositoryDirectory = null,
		TimeSpan? gitTimeout = null)
		=> new(
			StartedUtc: DateTime.UtcNow,
			OSDescription: RuntimeInformation.OSDescription,
			ProcessorCount: Environment.ProcessorCount,
			RuntimeVersion: RuntimeInformation.FrameworkDescription,
			InformationalVersion: GetEntryAssemblyInformationalVersion(),
			GitCommitHash: TryGetGitCommitHash(repositoryDirectory ?? Environment.CurrentDirectory, gitTimeout),
			GCMode: GCSettings.IsServerGC ? "Server" : "Workstation",
			SchemeConfig: schemeConfig is null ? null : SchemeConfigSnapshot.From(schemeConfig),
			Seed: seed);

	private static string? GetEntryAssemblyInformationalVersion()
		=> Assembly.GetEntryAssembly()
			?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
			?.InformationalVersion;

	/// <summary>
	/// Attempts to discover the current git commit hash by shelling out to
	/// <c>git rev-parse HEAD</c> in <paramref name="workingDirectory"/>.
	/// </summary>
	/// <remarks>
	/// Never throws: a missing git executable, a non-zero exit (e.g. not inside a repository), a
	/// timeout, or any other exception all result in <see langword="null"/>. This method must
	/// never be the reason a benchmark/run harness crashes.
	/// </remarks>
	/// <param name="workingDirectory">Directory to run the command from.</param>
	/// <param name="timeout">Maximum time to wait for the process to exit; defaults to <see cref="DefaultGitTimeout"/>.</param>
	/// <returns>The 40-character commit hash, or <see langword="null"/> when it could not be determined.</returns>
	public static string? TryGetGitCommitHash(string workingDirectory, TimeSpan? timeout = null)
	{
		if (string.IsNullOrEmpty(workingDirectory))
			return null;

#pragma warning disable CA1031 // Do not catch general exception types: this must never throw.
		try
		{
			if (!Directory.Exists(workingDirectory))
				return null;

			var startInfo = new ProcessStartInfo
			{
				FileName = "git",
				WorkingDirectory = workingDirectory,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};
			startInfo.ArgumentList.Add("rev-parse");
			startInfo.ArgumentList.Add("HEAD");

			using Process? process = Process.Start(startInfo);
			if (process is null)
				return null;

			int timeoutMs = (int)(timeout ?? DefaultGitTimeout).TotalMilliseconds;
			if (!process.WaitForExit(timeoutMs))
			{
				TryKill(process);
				return null;
			}

			if (process.ExitCode != 0)
				return null;

			string output = process.StandardOutput.ReadToEnd().Trim();
			return output.Length == 0 ? null : output;
		}
		catch (Exception)
		{
			return null;
		}
#pragma warning restore CA1031
	}

	private static void TryKill(Process process)
	{
#pragma warning disable CA1031 // Do not catch general exception types: best-effort cleanup only.
		try { process.Kill(entireProcessTree: true); }
		catch (Exception) { /* best-effort; process may have already exited. */ }
#pragma warning restore CA1031
	}

	/// <summary>
	/// Serializes <paramref name="manifest"/> as indented JSON and writes it to <paramref name="path"/>,
	/// overwriting any existing file.
	/// </summary>
	public static void Write(string path, RunManifest manifest)
	{
		ArgumentException.ThrowIfNullOrEmpty(path);
		ArgumentNullException.ThrowIfNull(manifest);
		File.WriteAllText(path, JsonSerializer.Serialize(manifest, JsonOptions));
	}
}
