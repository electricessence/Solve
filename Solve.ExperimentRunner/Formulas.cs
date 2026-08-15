using BlackBoxFunction;

namespace Solve.ExperimentRunner;

/// <summary>
/// A fixed, named set of target formulas a <c>"blackbox"</c> experiment definition can select by
/// name. Deliberately duplicated (not referenced) from the equivalent registry in
/// <c>Problems/BlackBoxFunction/Benchmark/Formulas.cs</c> -- that type is <see langword="internal"/>
/// to its own project, and both it and this copy are themselves duplicated from the private static
/// formula delegates in <see cref="global::BlackBoxFunction.Runner"/> (out of scope for
/// modification). Keep this set in lockstep manually if either sibling changes.
/// </summary>
internal static class Formulas
{
	static double AB(IReadOnlyList<double> p)
	{
		double a = p[0];
		double b = p[1];
		return a * b;
	}

	static double F3A2BC(IReadOnlyList<double> p)
	{
		double a = p[0];
		double b = p[1];
		double c = p[2];
		return 3 * a + 2 * b + c;
	}

	static double A2B2(IReadOnlyList<double> p)
	{
		double a = p[0];
		double b = p[1];
		return a * a + b * b;
	}

	static double SqrtA2B2(IReadOnlyList<double> p)
	{
		double a = p[0];
		double b = p[1];
		return Math.Sqrt(a * a + b * b);
	}

	static double SqrtA2B2C2(IReadOnlyList<double> p)
	{
		double a = p[0];
		double b = p[1];
		double c = p[2];
		return Math.Sqrt(a * a + b * b + c * c);
	}

	static double SqrtA2B2A2B1(IReadOnlyList<double> p)
	{
		double a = p[0];
		double b = p[1];
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
