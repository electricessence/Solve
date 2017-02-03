/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/evaluation-framework/blob/master/LICENSE.txt
 */

namespace EvaluationFramework
{
	public interface IParameter<TContext, TResult> : IEvaluate<TContext, TResult>
	{
		ushort ID { get; }
	}
}