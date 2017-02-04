using System;
using System.Collections.Generic;
using System.Linq;

namespace EvaluationFramework.Classes
{
	public static class ReductionExtensions
	{
		public static IEnumerable<IEvaluate<TContext, TResult>> Flatten<TFlat, TContext, TResult>(this IEnumerable<IEvaluate<TContext, TResult>> source)
			where TFlat : class, IParent<IEvaluate<TContext, TResult>>
		{

			// Phase 1: Flatten products of products.
			foreach (var child in source)
			{
				var r = child as IReducibleEvaluation<TContext, TResult>;
				var c = r?.AsReduced() ?? child;
				var f = c as TFlat;
				if (f == null)
				{
					yield return c;
				}
				else
				{
					foreach (var sc in f.Children)
						yield return sc;
				}
			}
		}

		public static Constant<TContext,TResult>[] ExtractConstants<TContext, TResult>(this List<IEvaluate<TContext,TResult>> target)
			where TResult : IComparable
		{
			var constants = target.OfType<Constant<TContext, TResult>>().ToArray();
			foreach (var c in constants)
				target.Remove(c);

			return constants;
		}
	}
}
