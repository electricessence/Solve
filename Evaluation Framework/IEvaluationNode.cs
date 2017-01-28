/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/Genetic-Algorithm-Platform/blob/master/LICENSE.md
 */

namespace EvaluationFramework
{
	public interface IEvaluationNode<TChild, in TContext, out TResult>
		: IParent<TChild>, IEvaluate<TContext, TResult>
		where TChild : IEvaluate<TContext, TResult>
	{

	}
}