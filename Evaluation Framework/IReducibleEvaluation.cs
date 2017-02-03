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
}
