/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using System.Runtime.CompilerServices;

namespace Solve.Dashboard.Web.Telemetry;

/// <summary>
/// Tails a JSONL run-event file (see <c>Solve/Telemetry/RunEventLog.schema.md</c>): replays every
/// complete line already on disk, in file order, then keeps polling for growth and yields newly
/// appended lines as they land.
/// </summary>
/// <remarks>
/// <para>
/// <b>Partial lines never terminate the stream.</b> A line that has not finished arriving -- the
/// file's normal growing tail, or a torn/truncated read -- is held back rather than yielded or
/// treated as an error; it is retried on a later poll once its terminating newline has arrived (see
/// <see cref="JsonlLines.SplitComplete"/>). This is what lets <c>GET /events</c> keep streaming a
/// live run indefinitely instead of faulting the moment it catches up to a writer mid-flush.
/// </para>
/// <para>
/// <b>Deliberately independent of <c>Solve.Telemetry.RunEvent</c></b> and the engine assembly: this
/// host is a sidecar that only needs to understand the JSONL wire format documented in
/// <c>RunEventLog.schema.md</c>, not the writer's C# types, so it works identically for live runs,
/// crashed runs, and archived runs, and has no compile-time dependency on the engine project.
/// </para>
/// </remarks>
public sealed class EventFileTailer
{
	private readonly string _path;
	private readonly TimeSpan _pollInterval;

	/// <param name="path">Path to the JSONL file to tail. Need not exist yet at construction time.</param>
	/// <param name="pollInterval">
	/// How often to check for file growth once caught up to the end of the file. Defaults to 200ms --
	/// comfortably inside the sub-second latency <c>GET /events</c> requires.
	/// </param>
	public EventFileTailer(string path, TimeSpan? pollInterval = null)
	{
		ArgumentException.ThrowIfNullOrEmpty(path);
		_path = path;
		_pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(200);
	}

	/// <summary>
	/// Replays history then follows growth, forever (or until <paramref name="cancellationToken"/> is
	/// cancelled / the enumeration is disposed). Each call starts its own independent read of the
	/// file from byte zero, so multiple concurrent callers (e.g. multiple SSE clients) each get their
	/// own full replay-then-follow view without coordinating with one another.
	/// </summary>
	public async IAsyncEnumerable<string> TailAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		long position = 0;
		byte[] leftover = [];

		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();

			List<string>? lines = null;

			if (File.Exists(_path))
			{
				try
				{
					using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
					long length = stream.Length;

					if (length < position)
					{
						// RunEventLog overwrites its output file (FileMode.Create) at the start of a
						// run -- a file now shorter than what we already consumed means a new run
						// started under the same path. Start over from the top.
						position = 0;
						leftover = [];
					}

					if (length > position)
					{
						stream.Seek(position, SeekOrigin.Begin);
						int toRead = checked((int)(length - position));
						byte[] buffer = new byte[toRead];
						int totalRead = await ReadFullyAsync(stream, buffer, cancellationToken).ConfigureAwait(false);

						byte[] combined = new byte[leftover.Length + totalRead];
						Buffer.BlockCopy(leftover, 0, combined, 0, leftover.Length);
						Buffer.BlockCopy(buffer, 0, combined, leftover.Length, totalRead);

						lines = JsonlLines.SplitComplete(combined, combined.Length, out leftover);
						position += totalRead;
					}
				}
				catch (IOException)
				{
					// Momentary sharing violation (writer mid-flush) or the file briefly missing
					// during a rotation -- retry on the next poll rather than tearing down the stream.
				}
			}

			if (lines is { Count: > 0 })
			{
				foreach (string line in lines)
					yield return line;
			}
			else
			{
				await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private static async Task<int> ReadFullyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
	{
		int total = 0;
		while (total < buffer.Length)
		{
			int read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false);
			if (read == 0) break; // Shouldn't happen given Length was already snapshotted, but guard regardless.
			total += read;
		}
		return total;
	}
}
