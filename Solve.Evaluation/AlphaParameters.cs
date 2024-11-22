using Open.Text;

namespace Solve.Evaluation;

public static class AlphaParameters
{
	private const string ALPHABET = "abcdefghijklmnopqrstuvwxyz";
	private static readonly char[] VARIABLE_NAMES = ALPHABET.ToCharArray();

	public static string ConvertTo(string source) => source.Supplant(VARIABLE_NAMES);
}
