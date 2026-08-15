using Solve.ExperimentRunner;
using System.Text.Json;

// Config-driven batch experiment runner.
//
// Reads every *.json definition file in a directory, runs each sequentially through the same
// headless event/status/summary discipline as Problems/Eater/Benchmark and
// Problems/BlackBoxFunction/Benchmark (see ExperimentRun.cs), and appends a one-line record per
// completed (or failed) run to a shared results-index.jsonl so a batch queued overnight can be
// compared afterward in one place. See README.md for the definition JSON schema.
//
// Usage: Solve.ExperimentRunner <definitionsDirectory> [resultsIndexPath]
//   definitionsDirectory  Directory containing one or more *.json experiment definitions.
//   resultsIndexPath      Where to append index records. Defaults to "results-index.jsonl"
//                          in the current working directory.

if (args.Length == 0)
{
	Console.WriteLine("Usage: Solve.ExperimentRunner <definitionsDirectory> [resultsIndexPath]");
	Environment.Exit(1);
	return;
}

string definitionsDirectory = args[0];
if (!Directory.Exists(definitionsDirectory))
{
	Console.WriteLine("Definitions directory not found: {0}", definitionsDirectory);
	Environment.Exit(1);
	return;
}

string resultsIndexPath = args.Length > 1 ? args[1] : "results-index.jsonl";

string[] definitionFiles = [.. Directory.GetFiles(definitionsDirectory, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase)];
if (definitionFiles.Length == 0)
{
	Console.WriteLine("No *.json definition files found in {0}", definitionsDirectory);
	return;
}

Console.WriteLine("Solve.ExperimentRunner: {0} definition(s) in {1}", definitionFiles.Length, definitionsDirectory);
Console.WriteLine("Results index: {0}", Path.GetFullPath(resultsIndexPath));

foreach (string file in definitionFiles)
{
	string fileName = Path.GetFileName(file);
	Console.WriteLine();
	Console.WriteLine("===== {0} =====", fileName);

	ExperimentDefinition definition;
	try
	{
		definition = ExperimentDefinitionReader.Read(file);
	}
	catch (ExperimentDefinitionException ex)
	{
		// A malformed definition fails only this run -- the batch continues with the next file.
		Console.WriteLine("[definition error] {0}", ex.Message);
		AppendIndexRecord(resultsIndexPath, ExperimentIndexRecord.Failed(fileName, fileName, ex.Message));
		continue;
	}

	try
	{
		ExperimentIndexRecord record = await ExperimentRun.ExecuteAsync(definition, fileName).ConfigureAwait(false);
		AppendIndexRecord(resultsIndexPath, record);
	}
#pragma warning disable CA1031 // Do not catch general exception types: one run's fault must not abort the batch.
	catch (Exception ex)
	{
		Console.WriteLine("[run error] {0}", ex);
		AppendIndexRecord(resultsIndexPath, ExperimentIndexRecord.Failed(definition.Name, fileName, ex.Message));
	}
#pragma warning restore CA1031
}

Console.WriteLine();
Console.WriteLine("Batch complete.");

// A prior run's scheme can leave background work stalled past its cancellation grace period (a
// known behavior of the underlying engine -- see ExperimentRun.cs); force the process down rather
// than risk hanging after the last definition.
Environment.Exit(0);

static void AppendIndexRecord(string path, ExperimentIndexRecord record)
{
	string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
	if (!string.IsNullOrEmpty(directory))
		Directory.CreateDirectory(directory);

	string line = JsonSerializer.Serialize(record);
	File.AppendAllText(path, line + Environment.NewLine);
}
