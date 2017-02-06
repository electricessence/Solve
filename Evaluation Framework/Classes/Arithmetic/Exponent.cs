/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/evaluation-framework/blob/master/LICENSE.txt
 */

using System;
using System.Collections.Generic;

namespace EvaluationFramework.ArithmeticOperators
{
	public class Exponent<TContext, TResult, TPower> : FunctionBase<TContext, TResult>
		where TResult : struct, IComparable
		where TPower : struct, IComparable
	{
		public Exponent(
			IEvaluate<TContext, TResult> evaluation,
			IEvaluate<TContext, TPower> power)
			: base(Exponent.SYMBOL, Exponent.SEPARATOR, evaluation)
		{
			Power = power;
		}

		public IEvaluate<TContext, TPower> Power
		{
			get;
			private set;
		}

		protected static double ConvertToDouble(dynamic value)
		{
			return (double)value;
		}
		public override TResult Evaluate(TContext context)
		{
			var evaluation = ConvertToDouble(base.Evaluate(context));
			var power = ConvertToDouble(Power.Evaluate(context));

			return (TResult)(dynamic)Math.Pow(evaluation, power);
		}

		protected override string ToStringRepresentationInternal()
		{
			return ToStringInternal(Evaluation.ToStringRepresentation(), Power.ToStringRepresentation());
		}

		public override IEvaluate<TContext, TResult> Reduction()
		{
			var pow = Power.AsReduced();
			var cPow = pow as Constant<TContext, TResult>;
			if (cPow != null)
			{
				dynamic p = cPow.Value;
				if (p == 0) return new Constant<TContext, TResult>((dynamic)1);
				if (p == 1) return Evaluation.AsReduced();
			}

			var result = new Exponent<TContext, TResult, TPower>(Evaluation.AsReduced(), pow);
			return result.ToStringRepresentation() == result.ToStringRepresentation() ? null : result;
		}

		protected string ToStringInternal(object contents, object power)
		{
			return string.Format("({0}^{1})", contents, power);
		}

	}

	public class Exponent<TContext, TResult> : Exponent<TContext, TResult, TResult>
		where TResult : struct, IComparable
	{
		public Exponent(
			IEvaluate<TContext, TResult> evaluation,
			IEvaluate<TContext, TResult> power) : base(evaluation, power)
		{
		}
	}

	public class Exponent<TContext> : Exponent<TContext, double>
	{
		public Exponent(
			IEvaluate<TContext, double> evaluation,
			IEvaluate<TContext, double> power) : base(evaluation, power)
		{
		}
	}

	public class Exponent : Exponent<IReadOnlyList<double>,double>
	{
		public const char SYMBOL = '^';
		public const string SEPARATOR = "^";

		public Exponent(IEvaluate<IReadOnlyList<double>, double> evaluation, IEvaluate<IReadOnlyList<double>, double> power) : base(evaluation, power)
		{
		}
	}

	// Can handle better precision operations that are only positive integers.
	// Because any fractional or negative exponents can introduce precision error. 
	public class IntegerExponent<TContext, TResult, TPower> : Exponent<TContext, TResult, TPower>
		where TResult : struct, IComparable
		where TPower : struct, IComparable
	{
		public IntegerExponent(
			IEvaluate<TContext, TResult> evaluation,
			IEvaluate<TContext, TPower> power) : base(evaluation, power)
		{
			if (!IsIntergerType(typeof(TPower), out IsSignedPowerType))
				throw new InvalidOperationException("Incompatible power type for IntegerExponent.");
		}

		protected static bool IsIntergerType(Type type, out bool isSigned)
		{
			isSigned
				= type == typeof(long)
				|| type == typeof(int)
				|| type == typeof(short)
				|| type == typeof(sbyte);

			return isSigned
				|| type == typeof(uint)
				|| type == typeof(ushort)
				|| type == typeof(byte);
		}

		protected static bool IsIntergerType(Type type)
		{
			bool isSigned;
			return IsIntergerType(type, out isSigned);
		}

		readonly bool IsSignedPowerType;

		// Why is this good?  Because it avoids precision errors that would occur with the default double precision math.
		// It also avoids any type conversion.  Integers stay integers, floats stay floats and decimals stay decimals.
		// This makes a lot of sense when considering how common a number to the power of a positive integer is.
		public override TResult Evaluate(TContext context)
		{
			dynamic value = Evaluation.Evaluate(context);
			if (value == 0 || value == 1) return value;

			dynamic power = Power.Evaluate(context);
			if (value == 1) return value;
			if (value == 0) return (TResult)(dynamic)1;

			var isNegativePower = IsSignedPowerType && power < 0;
			if (isNegativePower)
			{
				if (IsIntergerType(typeof(TResult)))
				{
					// Futile to divide integer 1 by another integer;
					throw new InvalidOperationException("Applying a negative exponent to an integer type will always result in 0.");
				}

				for (long i = 0; i > power; i--) value *= value;

				return 1 / value;
			}
			else
			{
				for (ulong i = 0; i < power; i++) value *= value;

				return value;
			}
		}

	}

}