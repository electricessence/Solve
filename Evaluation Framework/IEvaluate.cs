/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/Genetic-Algorithm-Platform/blob/master/LICENSE.md
 */

namespace EvaluationFramework
{
	public interface IEvaluate
	{
		object Evaluate(object context);

		string ToString(object context);

		string ToStringRepresentation();
	}

	public interface IEvaluate<out TResult> : IEvaluate
	{
		new TResult Evaluate(object context);
	}
	
}