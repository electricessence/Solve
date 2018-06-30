/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Solve
{
	/// <summary>
	/// Problems define what parameters need to be tested to resolve fitness.
	/// </summary>
	public interface IProblem<TGenome>
		 where TGenome : IGenome
	{
		int ID { get; }

		// 0 = acquire sampleId from sample count.  Negative numbers are allowed.
		double[] ProcessTest(TGenome g, long sampleId = 0);
		Task<double[]> ProcessTestAsync(TGenome g, long sampleId = 0);

		long TestCount { get; }

		IReadOnlyList<string> FitnessLabels { get; }

		RankedPool<TGenome> ChampionPool { get; }
	}


}
