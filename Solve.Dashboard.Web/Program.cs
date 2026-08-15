/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using Solve.Dashboard.Web.Telemetry;

// Documented in README.md's "Launch" section -- keep the two in sync if this ever changes.
const int DefaultPort = 5299;

string? eventsPath = null;
int port = DefaultPort;

for (int i = 0; i < args.Length; i++)
{
	string arg = args[i];
	if (arg is "--port")
	{
		if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out port))
		{
			Console.Error.WriteLine("--port requires a numeric value.");
			return 1;
		}
		i++;
	}
	else if (eventsPath is null)
	{
		eventsPath = arg;
	}
}

if (string.IsNullOrWhiteSpace(eventsPath))
{
	Console.Error.WriteLine("Usage: dotnet run --project Solve.Dashboard.Web -- <path-to-events.jsonl> [--port NNNN]");
	return 1;
}

string resolvedEventsPath = Path.GetFullPath(eventsPath);

// The file path and --port are consumed by hand above (the file path is positional, not a switch
// ASP.NET Core's own arg parsing would understand) -- CreateBuilder() is deliberately called with no
// args so it never sees them.
WebApplicationBuilder builder = WebApplication.CreateBuilder();
builder.WebHost.UseUrls($"http://localhost:{port}");

WebApplication app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/events", async (HttpContext context, CancellationToken cancellationToken) =>
{
	context.Response.ContentType = "text/event-stream";
	context.Response.Headers.CacheControl = "no-cache";
	context.Response.Headers.Append("X-Accel-Buffering", "no"); // hint to any reverse proxy: do not buffer this response.
	await context.Response.StartAsync(cancellationToken).ConfigureAwait(false);

	var tailer = new EventFileTailer(resolvedEventsPath);
	try
	{
		await foreach (string line in tailer.TailAsync(cancellationToken).ConfigureAwait(false))
		{
			await context.Response.WriteAsync($"data: {line}\n\n", cancellationToken).ConfigureAwait(false);
			await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
		}
	}
	catch (OperationCanceledException)
	{
		// Client disconnected, or the host is shutting down -- normal SSE teardown, nothing to do.
	}
});

app.MapGet("/api/summary", () =>
{
	IReadOnlyList<string> lines = JsonlFile.ReadCompleteLines(resolvedEventsPath);
	return Results.Json(RunSummaryAggregator.Aggregate(lines));
});

app.Run();

return 0;
