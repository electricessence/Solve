using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EvaluationFramework
{
	public interface IReducibleEvaluation<in TContext, out TResult> : IEvaluate<TContext, TResult>
	{
		/// <returns>Null if no reduction possible.  Otherwise returns the reduction.</returns>
		IEvaluate<TContext,TResult> Reduction();

		/// <returns>The reduced version if possible otherwise returns the current instance.</returns>
		IEvaluate<TContext, TResult> AsReduced();
	}

	public static class ReductionExtensions
	{
		public static IEvaluate<TContext, TResult> Reduction<TContext, TResult>(this IEvaluate<TContext, TResult> target)
		{
			return (target as IReducibleEvaluation<TContext, TResult>)?.Reduction();
		}

		public static IEvaluate<TContext, TResult> AsReduced<TContext, TResult>(this IEvaluate<TContext, TResult> target)
		{
			return (target as IReducibleEvaluation<TContext, TResult>)?.AsReduced() ?? target;
		}

		public static IEnumerable<IEvaluate<TContext, TResult>> Flatten<TFlat, TContext, TResult>(this IEnumerable<IEvaluate<TContext, TResult>> source)
			where TFlat : class, IParent<IEvaluate<TContext, TResult>>
		{

			// Phase 1: Flatten products of products.
			foreach (var child in source)
			{
				var c = child.AsReduced();
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

		public static Constant<TContext, TResult>[] ExtractConstants<TContext, TResult>(this List<IEvaluate<TContext, TResult>> target)
			where TResult : IComparable
		{
			var constants = target.OfType<Constant<TContext, TResult>>().ToArray();
			foreach (var c in constants)
				target.Remove(c);

			return constants;
		}
	}
}
