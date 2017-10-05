/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

namespace Solve
{
	public interface IReducible<T>
	{
		T AsReduced(bool ensureClone = false);

		bool IsReducible { get; }
	}
}