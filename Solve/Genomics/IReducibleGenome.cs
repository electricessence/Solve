/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

namespace Solve
{
	public interface IReducibleGenome<TGenome> : IReducible<TGenome>, IGenome
		where TGenome : IGenome
	{
		void ReplaceReduced(in TGenome reduced);
	}
}
