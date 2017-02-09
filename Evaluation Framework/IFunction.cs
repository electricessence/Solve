/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/Genetic-Algorithm-Platform/blob/master/LICENSE.md
 */

namespace EvaluationFramework
{
	public interface IFunction<out TResult>
		: IEvaluate<TResult>, ISymbolized
	{
	}
}