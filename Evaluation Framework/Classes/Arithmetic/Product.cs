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
			// Phase 1: Flatten products of products.
			var children = ChildrenInternal.Flatten<Product<TContext, TResult>, TContext, TResult>().ToList();

			// Phase 2: Can we collapse?
			switch (children.Count)
			{
				case 0:
					throw new InvalidOperationException("Cannot reduce product of empty set.");
				case 1:
					return children[0];
			}

			// Phase 3&4: Sum compatible exponents together.
			foreach (var exponents in children.OfType<Exponent<TContext, TResult>>()
				.GroupBy(g => g.Evaluation.ToStringRepresentation())
				.Where(g => g.Count() > 1))
			{
				var e1 = exponents.First();
				var power = new Sum<TContext, TResult>(exponents.Select(t => t.Power));
				foreach (var e in exponents)
					children.Remove(e);

				children.Add(new Exponent<TContext, TResult>(e1.Evaluation,power.AsReduced()));
			}

			// Phase 5: Combine constants.
			var constants = children.ExtractConstants();
			if (constants.Length != 0)
				children.Add(constants.Length == 1 ? constants[0] : constants.Product());

			// Phase 6: Check if collapsable?
			if (children.Count == 1)
				return children[0];

			// Lastly: Sort and return if different.
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