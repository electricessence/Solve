/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/Genetic-Algorithm-Platform/blob/master/LICENSE.md
 */

namespace EvaluationFramework
{
	public interface IOperator<TChild, in TContext, out TResult>
		: IFunction<TContext, TResult>, IEvaluationNode<TChild, TContext, TResult>, ISymbolized
		where TChild : IEvaluate<TContext, TResult>
	{ 

	}
}