/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/evaluation-framework/blob/master/LICENSE.txt
 */

using System;

namespace EvaluationFramework
{
	public interface IConstant<out TResult> : IEvaluate<TResult>
		where TResult : IComparable
	{
		TResult Value { get; }
	}
}