using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace EvaluationFramework
{
    public abstract class OperatorBase<TChild, TContext, TResult>
		: OperationBase<TContext, TResult>, IOperator<TChild, TContext, TResult>
		where TChild : IEvaluate<TContext, TResult>
		where TResult : IComparable
	{

		protected OperatorBase(string symbol, IEnumerable<TChild> children = null) : base(symbol)
		{
			ChildrenInternal = children == null ? new List<TChild>() : new List<TChild>(children);
			ReorderChildren();
			Children = ChildrenInternal.AsReadOnly();
		}

		protected readonly List<TChild> ChildrenInternal;

		public IReadOnlyList<TChild> Children
		{
			get;
			private set;
		}

		protected virtual void ReorderChildren()
		{
			ChildrenInternal.Sort(Compare);
		}

		protected override string ToStringInternal(object contents)
		{
			var collection = contents as IEnumerable;
			if (contents == null) return base.ToStringInternal(contents);
			var result = new StringBuilder('(');
			int index = -1;
			foreach (var o in collection)
			{
				if (++index != 0) result.Append(Symbol);
				result.Append(o);
			}
			result.Append(')');
			return result.ToString();
		}

		protected IEnumerable<TResult> ChildResults(TContext context)
		{
			foreach (var child in ChildrenInternal)
				yield return child.Evaluate(context);
		}

		protected IEnumerable<string> ChildRepresentations()
		{
			foreach (var child in ChildrenInternal)
				yield return child.ToStringRepresentation();
		}

		protected override string ToStringRepresentationInternal()
		{
			return ToStringInternal(ChildRepresentations());
		}


		// Need a standardized way to order so that comparisons are easier.
		protected static int Compare(TChild a, TChild b)
		{

			if (a is Constant<TContext,TResult> && !(b is Constant<TContext, TResult>))
				return 1;

			if (b is Constant<TContext, TResult> && !(a is Constant<TContext, TResult>))
				return -1;

			var aC = a as Constant<TContext, TResult>;
			var bC = b as Constant<TContext, TResult>;
			if (aC != null && bC != null)
				return bC.Value.CompareTo(aC.Value); // Descending...

			if (a is Parameter<TContext, TResult> && !(b is Parameter<TContext, TResult>))
				return 1;

			if (b is Parameter<TContext, TResult> && !(a is Parameter<TContext, TResult>))
				return -1;

			var aP = a as Parameter<TContext, TResult>;
			var bP = b as Parameter<TContext, TResult>;
			if (aP != null && bP != null)
				return aP.ID.CompareTo(bP.ID);

			var ats = a.ToStringRepresentation();
			var bts = b.ToStringRepresentation();

			return String.Compare(ats, bts);
		}


	}


}