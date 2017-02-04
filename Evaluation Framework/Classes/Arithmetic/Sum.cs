using System;
using System.Collections.Generic;
using System.Linq;

namespace EvaluationFramework.ArithmeticOperators
{
	public class Sum<TContext, TResult> : OperatorBase<IEvaluate<TContext, TResult>, TContext, TResult>
		where TResult : struct, IComparable
	{
		public const string SYMBOL = " + ";
		public Sum(IEnumerable<IEvaluate<TContext, TResult>> children = null)
			: base(SYMBOL, children)
		{

		}

		public override TResult Evaluate(TContext context)
		{
			if (ChildrenInternal.Count == 0)
				throw new InvalidOperationException("Cannot resolve sum of empty set.");

			dynamic result = 0;
			foreach (var r in ChildResults(context))
			{
				result += r;
			}

			return result;
		}

		public override IEvaluate<TContext, TResult> Reduction()
		{
			var children = new List<IEvaluate<TContext, TResult>>();
			if (children.Count == 0)
				return new Constant<TContext, TResult>((TResult)(dynamic)0);

			// Phase 1: Flatten sums of sums.
			foreach (var child in ChildrenInternal)
			{
				var r = child as IReducibleEvaluation<TContext, TResult>;
				var c = r?.AsReduced() ?? child;
				var s = c as Sum<TContext, TResult>;
				if (s == null)
				{
					children.Add(c);
				}
				else
				{
					foreach (var sc in s.ChildrenInternal)
						children.Add(sc);
				}
			}

			// Phase 2: Combine constants.
			var constants = children.OfType<Constant<TContext, TResult>>().ToList();
			if(constants.Count>1)
			{
				foreach(var c in constants)
					children.Remove(c);

				children.Add(constants.Sum());
			}

			// Phase 3: Check if collapsable?
			if (children.Count == 1)
				return children[0];

			// Phase 4: Look for groupings: constant multplied products
			var productsWithConstants = new List<Tuple<string, Constant<TContext, TResult>, IEvaluate<TContext, TResult>, Product<TContext, TResult>>>();
			foreach(var p in children.OfType<Product<TContext,TResult>>())
			{
				Constant<TContext, TResult> multiple;
				var reduced = p.ReductionWithMutlipleExtracted(out multiple);
				if(multiple!=null)
				{
					productsWithConstants.Add(new Tuple<string, Constant<TContext, TResult>, IEvaluate<TContext, TResult>, Product<TContext, TResult>>(
						reduced.ToStringRepresentation(),
						multiple,
						reduced,
						p
					));
				}
			}

			// Phase 5: Replace multipliable products with single merged version.
			foreach (var p in productsWithConstants
				.GroupBy(g => g.Item1)
				.Where(g => g.Count() > 1))
			{
				var p1 = p.First();
				var reduced = p1.Item3;
				var multiple = p.Select(t=>t.Item2).Sum();
				foreach (var px in p.Select(t=>t.Item4))
					children.Remove(px);

				var replacement = new List<IEvaluate<TContext, TResult>>(p.Select(t => t.Item3));
				replacement.Add(multiple);
				children.Add(new Product<TContext, TResult>(replacement));
			}

			if (constants.Any())
				children.Add(constants.Sum());

			children.Sort(Compare);
			var result = new Sum<TContext, TResult>(children);

			return result.ToStringRepresentation() == result.ToStringRepresentation() ? null : result;
		}

	}

	public static class Sum
	{
		public static Sum<TContext, TResult> Of<TContext, TResult>(params IEvaluate<TContext, TResult>[] evaluations)
		where TResult : struct, IComparable
		{
			return new Sum<TContext, TResult>(evaluations);
		}
	}

	public static class SumExtensions
	{
		public static Constant<TContext, TResult> Sum<TContext, TResult>(this IEnumerable<Constant<TContext, TResult>> constants)
		where TResult : struct, IComparable
		{
			var list = constants as IList<Constant<TContext, TResult>> ?? constants.ToList();
			switch(list.Count)
			{
				case 0:
					return new Constant<TContext, TResult>((TResult)(dynamic)0);
				case 1:
					return list[0];
			}
			
			dynamic result = 0;
			foreach (var c in constants)
			{
				result += c.Value;
			}

			return new Constant<TContext, TResult>(result);
		}

		public static Sum<TContext, float> Sum<TContext>(this IEnumerable<IEvaluate<TContext, float>> evaluations)
		{
			return new Sum<TContext, float>(evaluations);
		}

		public static Sum<TContext, double> Sum<TContext>(this IEnumerable<IEvaluate<TContext, double>> evaluations)
		{
			return new Sum<TContext, double>(evaluations);
		}

		public static Sum<TContext, decimal> Sum<TContext>(this IEnumerable<IEvaluate<TContext, decimal>> evaluations)
		{
			return new Sum<TContext, decimal>(evaluations);
		}

		public static Sum<TContext, short> Sum<TContext>(this IEnumerable<IEvaluate<TContext, short>> evaluations)
		{
			return new Sum<TContext, short>(evaluations);
		}

		public static Sum<TContext, ushort> Sum<TContext>(this IEnumerable<IEvaluate<TContext, ushort>> evaluations)
		{
			return new Sum<TContext, ushort>(evaluations);
		}


		public static Sum<TContext, int> Sum<TContext>(this IEnumerable<IEvaluate<TContext, int>> evaluations)
		{
			return new Sum<TContext, int>(evaluations);
		}

		public static Sum<TContext, uint> Sum<TContext>(this IEnumerable<IEvaluate<TContext, uint>> evaluations)
		{
			return new Sum<TContext, uint>(evaluations);
		}

		public static Sum<TContext, long> Sum<TContext>(this IEnumerable<IEvaluate<TContext, long>> evaluations)
		{
			return new Sum<TContext, long>(evaluations);
		}

		public static Sum<TContext, ulong> Sum<TContext>(this IEnumerable<IEvaluate<TContext, ulong>> evaluations)
		{
			return new Sum<TContext, ulong>(evaluations);
		}

	}


}