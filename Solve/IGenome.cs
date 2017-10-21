/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */


using Open.Cloneable;

namespace Solve
{

	public interface IGenome : IFreezable, ICloneable
	{
		string Hash { get; }

		// Simply added for potential convienience.  Equals may create problems.
		bool Equivalent(IGenome other);
	}

}