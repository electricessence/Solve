using System;
using System.Collections.Generic;

namespace EvaluationFramework
{
	public class Constant<TContext, TResult>
		: EvaluationBase<TContext, TResult>, IConstant<TContext, TResult>, ICloneable
		where TResult : IComparable
	{

		public Constant(TResult value) : base()
		{
			Value = value;
		}

		public TResult Value
		{
			get;
			private set;
		}

		protected override string ToStringRepresentationInternal()
		{
			return string.Empty + Value;
		}

		public Constant<TContext, TResult> Clone()
		{
			return new Constant<TContext, TResult>(Value);
		}


		object ICloneable.Clone()
		{
			return this.Clone();
		}

		public override TResult Evaluate(TContext context)
		{
			return Value;
		}

		public override string ToString(TContext context)
		{
			return ToStringRepresentation();
		}

		public static Constant<TContext, TResult> operator +(Constant<TContext, TResult> a, Constant<TContext, TResult> b)
		{
			dynamic value = 0;
			value += a.Value;
			value += b.Value;
			return new Constant<TContext, TResult>(value);
		}

		public static Constant<TContext, TResult> operator *(Constant<TContext, TResult> a, Constant<TContext, TResult> b)
		{
			dynamic value = 1;
			value *= a.Value;
			value *= b.Value;
			return new Constant<TContext, TResult>(value);
		}

	}

	public sealed class Constant : Constant<IReadOnlyList<double>, double>
	{
		public Constant(double value) : base(value)
		{
		}
	}

}