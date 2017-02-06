using System;
using System.Collections.Generic;

namespace EvaluationFramework.BooleanOperators
{
	public class And<TContext> : OperatorBase<IEvaluate<TContext, bool>, TContext, bool>
	{
		public And(IEnumerable<IEvaluate<TContext, bool>> children = null)
			: base(And.SYMBOL, And.SEPARATOR, children)
		{

		}

		public override bool Evaluate(TContext context)
		{
			if (ChildrenInternal.Count == 0)
				throw new InvalidOperationException("Cannot resolve boolean of empty set.");

			foreach (var result in ChildResults(context))
			{
				if (!result) return false;
			}

			return true;
		}

	}


	public class And : And<IReadOnlyList<bool>>
	{
		public const char SYMBOL = '&';
		public const string SEPARATOR = " & ";

		public static And<TContext> Using<TContext>(IEnumerable<IEvaluate<TContext, bool>> evaluations)
		{
			return new And<TContext>(evaluations);
		}
	}

}