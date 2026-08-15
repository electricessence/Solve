/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using System.Text;

namespace Solve.Dashboard.Web.Telemetry;

/// <summary>
/// Shared line-splitting core for <see cref="EventFileTailer"/> (incremental, polling) and
/// <see cref="JsonlFile"/> (one-shot snapshot): splits a byte buffer into complete,
/// newline-terminated lines, decoding each as UTF-8 text with its terminator (<c>"\n"</c> or
/// <c>"\r\n"</c>) stripped.
/// </summary>
/// <remarks>
/// Bytes after the last <c>'\n'</c> -- a line still being written, or a deliberately truncated
/// fixture used to exercise that case -- are returned via <paramref name="leftover"/> (see
/// <see cref="SplitComplete"/>) instead of being included in the result, so a torn trailing line is
/// naturally "not here yet" rather than data a caller must remember to discard or that could be
/// mistaken for a malformed complete line.
/// </remarks>
internal static class JsonlLines
{
	/// <param name="data">Buffer to split. Only <paramref name="length"/> bytes of it are considered.</param>
	/// <param name="length">Number of valid bytes in <paramref name="data"/> (may be less than its full capacity).</param>
	/// <param name="leftover">Bytes after the last complete line's terminator -- possibly empty, never <see langword="null"/>.</param>
	public static List<string> SplitComplete(byte[] data, int length, out byte[] leftover)
	{
		var lines = new List<string>();
		int start = 0;
		int consumedThrough = 0;

		for (int i = 0; i < length; i++)
		{
			if (data[i] != (byte)'\n') continue;

			ReadOnlySpan<byte> line = data.AsSpan(start, i - start);
			if (line.Length > 0 && line[^1] == (byte)'\r')
				line = line[..^1];

			lines.Add(Encoding.UTF8.GetString(line));
			start = i + 1;
			consumedThrough = i + 1;
		}

		int leftoverLength = length - consumedThrough;
		leftover = leftoverLength == 0 ? [] : data.AsSpan(consumedThrough, leftoverLength).ToArray();
		return lines;
	}
}
