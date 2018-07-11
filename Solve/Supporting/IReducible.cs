/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

namespace Solve
{
	public interface IReducible
	{
		object AsReduced(bool ensureClone = false);

		bool IsReducible { get; }
	}

	public interface IReducible<T> : IReducible
	{
		new T AsReduced(bool ensureClone = false);
	}
}
