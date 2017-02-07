using System;
using System.Collections.Generic;
using System.Linq;
using Open;
using EvaluationFramework;
using EvaluationFramework.ArithmeticOperators;
using IOperator = EvaluationFramework.IOperator<EvaluationFramework.IEvaluate<System.Collections.Generic.IReadOnlyList<double>, double>, System.Collections.Generic.IReadOnlyList<double>, double>;

namespace BlackBoxFunction
{
	public static class Operators
	{
		// Operators...
		public const char ADD = Sum.SYMBOL;
		public const char MULTIPLY = Product.SYMBOL;

		// Functions. Division is simply an 'inversion'.
		public const char EXPONENT = Exponent.SYMBOL;

		public static class Available
		{
			public static readonly IReadOnlyList<char> Operators = (new List<char> { ADD, MULTIPLY }).AsReadOnly();
			public static readonly IReadOnlyList<char> Functions = (new List<char> { EXPONENT }).AsReadOnly();
		}
		

		public static char GetRandom(IEnumerable<char> excluded = null)
		{
			var ao = excluded == null
				? Available.Operators
				: Available.Operators.Where(o => !excluded.Contains(o)).ToArray();
			return ao.RandomSelectOne();
		}

		public static char GetRandom(char excluded)
		{
			var ao = Available.Operators.Where(o => o != excluded).ToArray();
			return ao.RandomSelectOne();
		}

		public static char GetRandomFunction()
		{
			return Available.Functions.RandomSelectOne();
		}

		public static char GetRandomFunction(char excluded)
		{
			var ao = Available.Functions.Where(o => o != excluded).ToArray();
			return ao.RandomSelectOne();
		}
		


	}
}