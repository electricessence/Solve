using System;
using System.Collections.Generic;

namespace EvaluationFramework.BooleanOperators
{
	public abstract class CountingBase<TContext> : OperatorBase<IEvaluate<TContext, bool>, TContext, bool>
	{

		protected CountingBase(string prefix, int count, IEnumerable<IEvaluate<TContext, bool>> children = null)
			: base(CountingBase.SYMBOL, CountingBase.SEPARATOR, children)
		{
			if (prefix == null)
				throw new ArgumentNullException("prefix");
			if (count < 0)
				throw new ArgumentOutOfRangeException("count", count, "Count must be at least 0.");
			Prefix = prefix;
			Count = count;
		}

		protected readonly string Prefix;

		public int Count
		{
			get;
			private set;
		}

		protected override string ToStringInternal(object contents)
		{
			return String.Format("{0}({1}, {2})", Prefix, Count, base.ToStringInternal(contents));
		}

	}

	public static class CountingBase
	{
		public const char SYMBOL = ',';
		public const string SEPARATOR = ", ";
	}

}