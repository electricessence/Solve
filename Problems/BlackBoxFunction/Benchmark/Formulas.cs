using BlackBoxFunction;

namespace BlackBoxFunction.Benchmark;

/// <summary>
/// A fixed, named set of target formulas the benchmark harness can be pointed at.
/// Deliberately duplicated (not referenced) from the private static formula delegates in
/// <see cref="global::BlackBoxFunction.Runner"/> — that file is out of scope for modification in
/// this task, and its formulas are private members of an internal class. Keep this set in
/// lockstep manually if Runner.cs's formulas change.
/// </summary>
internal static class Formulas
{
	static double AB(IReadOnlyList<double> p)
	{
		var a = p[0];
		var b = p[1];
		return a * b;
	}

	static double F3A2BC(IReadOnlyList<double> p)
	{
		var a = p[0];
		var b = p[1];
		var c = p[2];
		return 3 * a + 2 * b + c;
	}

	static double A2B2(IReadOnlyList<double> p)
	{
		var a = p[0];
		var b = p[1];
		return a * a + b * b;
	}

	static double SqrtA2B2(IReadOnlyList<double> p)
	{
		var a = p[0];
		var b = p[1];
		return Math.Sqrt(a * a + b * b);
	}

	static double SqrtA2B2C2(IReadOnlyList<double> p)
	{
		var a = p[0];
		var b = p[1];
		var c = p[2];
		return Math.Sqrt(a * a + b * b + c * c);
	}

	static double SqrtA2B2A2B1(IReadOnlyList<double> p)
	{
		var a = p[0];
		var b = p[1];
		return Math.Sqrt(a * a + b * b + a + 2) - b + 1;
	}

	public static readonly IReadOnlyDictionary<string, Formula> ByName
		= new Dictionary<string, Formula>(StringComparer.OrdinalIgnoreCase)
		{
			[nameof(AB)] = AB,
			[nameof(F3A2BC)] = F3A2BC,
			[nameof(A2B2)] = A2B2,
			[nameof(SqrtA2B2)] = SqrtA2B2,
			[nameof(SqrtA2B2C2)] = SqrtA2B2C2,
			[nameof(SqrtA2B2A2B1)] = SqrtA2B2A2B1,
		};
}
