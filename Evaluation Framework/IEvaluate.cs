/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/Genetic-Algorithm-Platform/blob/master/LICENSE.md
 */

namespace EvaluationFramework
{
	public interface IEvaluate<in TContext, out TResult>
	{
		TResult Evaluate(TContext context);

		string ToString(TContext context);

		string ToStringRepresentation();
	}
}