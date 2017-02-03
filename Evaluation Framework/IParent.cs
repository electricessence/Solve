/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/evaluation-framework/blob/master/LICENSE.txt
 */

using System.Collections.Generic;

namespace EvaluationFramework
{
	public interface IParent<TChild>
	{
		IReadOnlyList<TChild> Children { get; }
	}
}