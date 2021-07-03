using System;

namespace Solve.Metrics
{
	public struct SuccessFailKeys
	{
		public SuccessFailKeys(string prefix)
		{
			Prefix = string.Intern(prefix ?? throw new ArgumentNullException(nameof(prefix)));
			if (string.IsNullOrWhiteSpace(prefix))
				throw new ArgumentException("Cannot empty or be whitespace.", nameof(prefix));

			Succeded = string.Intern(Prefix + " SUCCEDED");
			Failed = string.Intern(Prefix + " FAILED");
		}

		public string Prefix { get; }
		public string Succeded { get; }
		public string Failed { get; }

		public string Switch(bool success)
			=> success ? Succeded : Failed;

		public static implicit operator SuccessFailKeys(string prefix)
			=> new(prefix);
	}
}
