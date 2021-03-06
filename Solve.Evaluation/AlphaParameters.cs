﻿using Open.Text;

namespace Solve.Evaluation
{
	public static class AlphaParameters
	{
		const string ALPHABET = "abcdefghijklmnopqrstuvwxyz";
		static readonly char[] VARIABLE_NAMES = ALPHABET.ToCharArray();

		public static string ConvertTo(string source)
		{
			return source.Supplant(VARIABLE_NAMES);
		}
	}
}
