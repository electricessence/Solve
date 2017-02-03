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


			foreach (var p in productsWithConstants
				.GroupBy(g => g.Item1)
				.Where(g => g.Count() > 1))
			{
				
			}

			if (constants.Any())
				children.Add(constants.Sum());

			children.Sort(Compare);
			var result = new Sum<TContext, TResult>(children);

			return result.ToStringRepresentation() == result.ToStringRepresentation() ? null : result;
		}




		protected override void ReduceLoop()
		{
			// Collapse sums within sums.
			var children = GetChildren();
			if (children.Count == 0) return;

			foreach (var p in children.OfType<SumGene>().ToArray())
			{
				var m = p.Multiple;
				foreach (var s in p)
				{
					s.Multiple *= m;
					children.Add(s);
				}
				p.Clear();
				children.Remove(p);
			}

			// Flatten negatives...
			if (Multiple < 0 && children.Any(c => c.Multiple < 0) || Multiple > 0 && children.All(c => c.Multiple < 0))
			{
				Multiple *= -1;
				foreach (var g in children)
					g.Multiple *= -1;
			}

			if (children.All(c =>
			{
				var cm = c.Multiple;
				return !double.IsNaN(cm) && !double.IsInfinity(cm);
			}))
			{
				// Pull out multiples.
				using (var absMultiples = children.Select(c => Math.Abs(c.Multiple)).Where(m => m != 0 && m != 1).Distinct().Memoize())
				{
					if (absMultiples.Any())
					{
						var max = absMultiples.Min();
						for (var i = 2; max != 1 && i <= max; i = i.NextPrime())
						{
							while (max % i == 0 && children.All(g => g.Multiple % i == 0))
							{
								max /= i;
								Multiple *= i;
								foreach (var g in children)
								{
									g.Multiple /= i;
								}
							}
						}

					}
				}
			}

			// Combine any constants.  This is more optimal because we don't neet to query ToStringContents.
			var constants = children.OfType<ConstantGene>().ToArray();
			if (constants.Length > 1)
			{
				var main = constants.First();
				foreach (var c in constants.Skip(1))
				{
					main.Multiple += c.Multiple;
					children.Remove(c);
				}
			}

			RemoveZeroMultiples();

			// Look for groupings...
			foreach (var p in children
				.Where(g => !(g is ConstantGene)) // We just reduced constants above so skip them...
				.GroupBy(g => g.ToStringUsingMultiple(1))
				.Where(g => g.Count() > 1))
			{
				using (var matches = p.Memoize())
				{
					// Take matching groupings and merge them.
					var main = matches.First();
					var sum = matches.Sum(s => s.Multiple);

					if (sum == 0)
						// Remove the gene that would remain with a zero.
						Remove(main);
					else
						main.Multiple = sum;

					// Remove the other genes that are now useless.
					foreach (var gene in matches.Skip(1))
						Remove(gene);

					break;
				}
			}

			RemoveZeroMultiples();
		}

		protected override IGene ReplaceWithReduced()
		{
			var children = GetChildren();
			switch (children.Count)
			{
				case 0:
					return new ConstantGene(0);
				case 1:
					var c = children.Single();
					c.Multiple *= Multiple;
					Remove(c);
					return c;
			}
			if (children.Any(c => double.IsNaN(c.Multiple)))
				return new ConstantGene(double.NaN);
			return base.ReplaceWithReduced();
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