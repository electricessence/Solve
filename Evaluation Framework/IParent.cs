/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/Genetic-Algorithm-Platform/blob/master/LICENSE.md
 */

using System.Collections.Generic;

namespace EvaluationFramework
{
	public interface IParent<TChild>
	{
		IReadOnlyList<TChild> Children { get; }
	}
}