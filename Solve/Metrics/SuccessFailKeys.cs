namespace Solve.Metrics;

public readonly record struct SuccessFailKeys
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

#pragma warning disable IDE0079 // Remove unnecessary suppression
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2225:Operator overloads have named alternates", Justification = "<Pending>")]
#pragma warning restore IDE0079 // Remove unnecessary suppression
	public static implicit operator SuccessFailKeys(string prefix)
		=> new(prefix);
}
