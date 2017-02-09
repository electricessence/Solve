/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/evaluation-framework/blob/master/LICENSE.txt
 */

namespace EvaluationFramework
{
	public interface IParameter<TResult> : IEvaluate<TResult>
	{
		ushort ID { get; }
	}
}