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

		IEnumerable<double[]> ProcessSample(TGenome g, long sampleId);
		Task<IEnumerable<double[]>> ProcessSampleAsync(TGenome g, long sampleId);

		long TestCount { get; }

		IReadOnlyList<string> FitnessLabels { get; }

		RankedPool<TGenome> ChampionPool { get; }
	}

}
