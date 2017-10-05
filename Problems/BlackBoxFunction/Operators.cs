using System;
using System.Collections.Generic;
using System.Linq;
using Open;
using Open.Evaluation;
using Open.Evaluation.ArithmeticOperators;
using IOperator = Open.Evaluation.IOperator<Open.Evaluation.IEvaluate<double>, double>;
using Open.Numeric;

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