/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

namespace Solve.Dashboard.Web.Telemetry;

/// <summary>
/// One-shot read of a JSONL file's currently-complete lines -- used by <c>GET /api/summary</c>,
/// which (unlike <c>GET /events</c>) wants a point-in-time snapshot rather than a live stream.
/// Shares <see cref="JsonlLines.SplitComplete"/> with <see cref="EventFileTailer"/> so both
/// endpoints treat a torn trailing line identically: silently omitted rather than surfaced as an
/// error or a malformed value.
/// </summary>
public static class JsonlFile
{
	/// <summary>
	/// Reads every complete (newline-terminated) line currently in <paramref name="path"/>, in file
	/// order. Returns an empty list if the file does not exist yet, or if it could not be opened
	/// (e.g. a momentary sharing violation while the writer is mid-flush) -- callers should treat
	/// that the same as "no events observed yet", not as an error.
	/// </summary>
	public static IReadOnlyList<string> ReadCompleteLines(string path)
	{
		ArgumentException.ThrowIfNullOrEmpty(path);
		if (!File.Exists(path)) return [];

		try
		{
			using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			var buffer = new byte[stream.Length];
			int total = 0;
			while (total < buffer.Length)
			{
				int read = stream.Read(buffer, total, buffer.Length - total);
				if (read == 0) break;
				total += read;
			}

			return JsonlLines.SplitComplete(buffer, total, out _);
		}
		catch (IOException)
		{
			return [];
		}
	}
}
