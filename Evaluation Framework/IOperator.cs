/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/Genetic-Algorithm-Platform/blob/master/LICENSE.md
 */

using System;

namespace EvaluationFramework
{
	public interface IOperator<out TChild, out TResult>
		: IFunction<TResult>, IParent<TChild>
		where TChild : IEvaluate
		where TResult : IComparable
	{ 

	}
}