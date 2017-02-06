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

		public static IOperator New(char op, IEnumerable<IEvaluate<IReadOnlyList<double>, double>> children, double modifier = 1)
		{

			switch (op)
			{

				case ADD:
					return new Sum(children);

				case MULTIPLY:
					return new Product(children);

				case EXPONENT:
					return new Exponent(multiple);
					
			}

			throw new ArgumentException("Invalid operator symbol.", "op");

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

		public static IOperator GetRandomOperationGene(IEnumerable<char> excluded = null)
		{
			return New(GetRandom(excluded));
		}

		public static IOperator GetRandomOperationGene(char excluded)
		{
			return New(GetRandom(excluded));
		}

		public static IOperator GetRandomFunctionGene(char excluded)
		{
			return New(GetRandomFunction(excluded));
		}


	}
}