using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace EvaluationFramework.ArithmeticOperators
{
	public class Product<TContext, TResult> : OperatorBase<IEvaluate<TContext, TResult>, TContext, TResult>
		where TResult : struct, IComparable
	{
		public const string SYMBOL = " + ";
		public Product(IEnumerable<IEvaluate<TContext, TResult>> children = null)
			: base(SYMBOL, children)
		{

		}

		public override TResult Evaluate(TContext context)
		{
			if (ChildrenInternal.Count == 0)
				throw new InvalidOperationException("Cannot resolve product of empty set.");

			dynamic result = 1;
			foreach (var r in ChildResults(context))
			{
				result *= r;
			}

			return result;
		}

		public override IEvaluate<TContext, TResult> Reduction()
		{
			var children = new List<IEvaluate<TContext, TResult>>();

			// Phase 1: Flatten sums of sums.
			foreach (var child in ChildrenInternal)
			{
				var r = child as IReducibleEvaluation<IEvaluate<TContext, TResult>>;
				var c = r?.AsReduced() ?? child;
				var p = c as Product<TContext, TResult>;
				if (p == null)
				{
					children.Add(c);
				}
				else
				{
					foreach (var sc in p.ChildrenInternal)
						children.Add(sc);
				}
			}

			// Phase 2: Combine constants.
			var constants = children.OfType<Constant<TContext, TResult>>().ToList();
			if (constants.Count > 1)
			{
				foreach (var c in constants)
					children.Remove(c);

				children.Add(constants.Product());
			}

			// Phase 3: Check if collapsable?
			if (children.Count == 1)
				return children[0];

			children.Sort(Compare);
			var result = new Product<TContext, TResult>(children);

			return result.ToStringRepresentation() == result.ToStringRepresentation() ? null : result;
		}

		public IEvaluate<TContext, TResult> ReductionWithMutlipleExtracted(out Constant<TContext, TResult> multiple)
		{
			multiple = null;
			var reduced = this.AsReduced();
			var product = reduced as Product<TContext, TResult>;
			if(product!=null)
			{
				var children = product.ChildrenInternal.ToList();
				var constants = product.ChildrenInternal.OfType<Constant<TContext, TResult>>().ToArray();
				Debug.Assert(constants.Length <= 1, "Reduction should have collapsed constants.");
				if (constants.Length == 0)
					return product;
				multiple = constants.Single();
				children.Remove(multiple);
				return new Product<TContext, TResult>(children);
			}
			return reduced;			
		}
	}

	public static class Product
	{
		public static Product<TContext, TResult> Of<TContext, TResult>(params IEvaluate<TContext, TResult>[] evaluations)
		where TResult : struct, IComparable
		{
			return new Product<TContext, TResult>(evaluations);
		}
	}

	public static class ProductExtensions
	{
		public static Constant<TContext, TResult> Product<TContext, TResult>(this IEnumerable<Constant<TContext, TResult>> constants)
			where TResult : struct, IComparable
		{
			var list = constants as IList<Constant<TContext, TResult>> ?? constants.ToList();
			switch (list.Count)
			{
				case 0:
					return new Constant<TContext, TResult>(default(TResult));
				case 1:
					return list[0];
			}

			dynamic result = 1;
			foreach (var c in constants)
			{
				result *= c.Value;
			}

			return new Constant<TContext, TResult>(result);
		}


		public static Product<TContext, float> Product<TContext>(this IEnumerable<IEvaluate<TContext, float>> evaluations)
		{
			return new Product<TContext, float>(evaluations);
		}

		public static Product<TContext, double> Product<TContext>(this IEnumerable<IEvaluate<TContext, double>> evaluations)
		{
			return new Product<TContext, double>(evaluations);
		}

		public static Product<TContext, decimal> Product<TContext>(this IEnumerable<IEvaluate<TContext, decimal>> evaluations)
		{
			return new Product<TContext, decimal>(evaluations);
		}

		public static Product<TContext, short> Product<TContext>(this IEnumerable<IEvaluate<TContext, short>> evaluations)
		{
			return new Product<TContext, short>(evaluations);
		}

		public static Product<TContext, ushort> Product<TContext>(this IEnumerable<IEvaluate<TContext, ushort>> evaluations)
		{
			return new Product<TContext, ushort>(evaluations);
		}


		public static Product<TContext, int> Product<TContext>(this IEnumerable<IEvaluate<TContext, int>> evaluations)
		{
			return new Product<TContext, int>(evaluations);
		}

		public static Product<TContext, uint> Product<TContext>(this IEnumerable<IEvaluate<TContext, uint>> evaluations)
		{
			return new Product<TContext, uint>(evaluations);
		}

		public static Product<TContext, long> Product<TContext>(this IEnumerable<IEvaluate<TContext, long>> evaluations)
		{
			return new Product<TContext, long>(evaluations);
		}

		public static Product<TContext, ulong> Product<TContext>(this IEnumerable<IEvaluate<TContext, ulong>> evaluations)
		{
			return new Product<TContext, ulong>(evaluations);
		}

	}


}