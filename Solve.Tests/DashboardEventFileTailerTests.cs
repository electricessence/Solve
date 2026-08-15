using Solve.Dashboard.Web.Telemetry;

namespace Solve.Tests;

/// <summary>
/// Coverage for 15-0033's tail logic (<see cref="EventFileTailer"/>): replay-then-follow semantics
/// (acceptance criterion 3), and -- acceptance criterion 4 -- that a truncated/partial trailing line
/// is held back rather than surfaced or treated as a stream-ending error.
/// </summary>
public class DashboardEventFileTailerTests
{
	private static string TempPath()
		=> Path.Combine(Path.GetTempPath(), $"dashboard-tail-{Guid.NewGuid():N}.jsonl");

	private static readonly TimeSpan FastPoll = TimeSpan.FromMilliseconds(20);
	private static readonly TimeSpan GenerousTimeout = TimeSpan.FromSeconds(10);

	[Fact]
	public async Task ReplaysPreExistingLinesInFileOrder()
	{
		string path = TempPath();
		try
		{
			await File.WriteAllTextAsync(path, "{\"type\":\"a\"}\n{\"type\":\"b\"}\n{\"type\":\"c\"}\n");

			var tailer = new EventFileTailer(path, FastPoll);
			await using IAsyncEnumerator<string> e = tailer.TailAsync().GetAsyncEnumerator();

			foreach (string expected in new[] { "{\"type\":\"a\"}", "{\"type\":\"b\"}", "{\"type\":\"c\"}" })
			{
				Assert.True(await e.MoveNextAsync().AsTask().WaitAsync(GenerousTimeout));
				Assert.Equal(expected, e.Current);
			}
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	[Fact]
	public async Task TruncatedTrailingLineIsHeldBackNotSkippedOrFatal()
	{
		string path = TempPath();
		try
		{
			// Two complete lines, then a trailing line with no terminator yet -- as if caught mid-write.
			await File.WriteAllTextAsync(path, "{\"type\":\"a\"}\n{\"type\":\"b\"}\n{\"type\":\"c\"");

			var tailer = new EventFileTailer(path, FastPoll);
			await using IAsyncEnumerator<string> e = tailer.TailAsync().GetAsyncEnumerator();

			Assert.True(await e.MoveNextAsync().AsTask().WaitAsync(GenerousTimeout));
			Assert.Equal("{\"type\":\"a\"}", e.Current);
			Assert.True(await e.MoveNextAsync().AsTask().WaitAsync(GenerousTimeout));
			Assert.Equal("{\"type\":\"b\"}", e.Current);

			// The partial third line must not appear even after several poll intervals: race the next
			// MoveNextAsync against a short timeout and confirm the timeout wins -- the tailer is
			// still waiting on it, not skipping the garbage tail as data and not ending the stream on it.
			Task<bool> pendingMoveNext = e.MoveNextAsync().AsTask();
			Task winner = await Task.WhenAny(pendingMoveNext, Task.Delay(TimeSpan.FromMilliseconds(300)));
			Assert.NotSame(pendingMoveNext, winner);

			// Now complete the line: the *same* pending task must resolve to it, proving it was truly
			// just deferred (not dropped, not requiring a fresh enumerator).
			await File.AppendAllTextAsync(path, "}\n");
			Assert.True(await pendingMoveNext.WaitAsync(GenerousTimeout));
			Assert.Equal("{\"type\":\"c\"}", e.Current);
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	[Fact]
	public async Task StreamsAppendedLinesAfterInitialReplay()
	{
		string path = TempPath();
		try
		{
			await File.WriteAllTextAsync(path, "{\"type\":\"a\"}\n");

			var tailer = new EventFileTailer(path, FastPoll);
			await using IAsyncEnumerator<string> e = tailer.TailAsync().GetAsyncEnumerator();

			Assert.True(await e.MoveNextAsync().AsTask().WaitAsync(GenerousTimeout));
			Assert.Equal("{\"type\":\"a\"}", e.Current);

			await File.AppendAllTextAsync(path, "{\"type\":\"b\"}\n");

			Assert.True(await e.MoveNextAsync().AsTask().WaitAsync(GenerousTimeout));
			Assert.Equal("{\"type\":\"b\"}", e.Current);

			await File.AppendAllTextAsync(path, "{\"type\":\"c\"}\n{\"type\":\"d\"}\n");

			Assert.True(await e.MoveNextAsync().AsTask().WaitAsync(GenerousTimeout));
			Assert.Equal("{\"type\":\"c\"}", e.Current);
			Assert.True(await e.MoveNextAsync().AsTask().WaitAsync(GenerousTimeout));
			Assert.Equal("{\"type\":\"d\"}", e.Current);
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	[Fact]
	public async Task ToleratesFileNotExistingYetThenPicksUpOnceCreated()
	{
		string path = TempPath(); // deliberately never created up front.
		try
		{
			var tailer = new EventFileTailer(path, FastPoll);
			await using IAsyncEnumerator<string> e = tailer.TailAsync().GetAsyncEnumerator();

			Task<bool> pendingMoveNext = e.MoveNextAsync().AsTask();
			Task winner = await Task.WhenAny(pendingMoveNext, Task.Delay(TimeSpan.FromMilliseconds(200)));
			Assert.NotSame(pendingMoveNext, winner); // still polling for the file to appear -- no exception, no false "end of stream".

			await File.WriteAllTextAsync(path, "{\"type\":\"a\"}\n");

			Assert.True(await pendingMoveNext.WaitAsync(GenerousTimeout));
			Assert.Equal("{\"type\":\"a\"}", e.Current);
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	[Fact]
	public async Task CarriageReturnLineFeedTerminatorsAreStrippedLikeBareLineFeeds()
	{
		string path = TempPath();
		try
		{
			await File.WriteAllTextAsync(path, "{\"type\":\"a\"}\r\n{\"type\":\"b\"}\r\n");

			var tailer = new EventFileTailer(path, FastPoll);
			await using IAsyncEnumerator<string> e = tailer.TailAsync().GetAsyncEnumerator();

			Assert.True(await e.MoveNextAsync().AsTask().WaitAsync(GenerousTimeout));
			Assert.Equal("{\"type\":\"a\"}", e.Current); // no trailing '\r'.
			Assert.True(await e.MoveNextAsync().AsTask().WaitAsync(GenerousTimeout));
			Assert.Equal("{\"type\":\"b\"}", e.Current);
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}
}
