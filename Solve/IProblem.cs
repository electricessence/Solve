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
		double[] ProcessSampleMetrics(TGenome g, long sampleId = 0);
		Task<double[]> ProcessSampleMetricsAsync(TGenome g, long sampleId = 0);

		long TestCount { get; }

		IReadOnlyList<string> FitnessLabels { get; }

		RankedPool<TGenome> ChampionPool { get; }
	}


	/// <summary>
	/// Problems define what parameters need to be tested to resolve fitness.
	/// </summary>
	public interface IProblem<TGenome, TMetrics> : IProblem<TGenome>
		 where TGenome : IGenome
	{
		// 0 = acquire sampleId from sample count.  Negative numbers are allowed.
		new TMetrics ProcessSampleMetrics(TGenome g, long sampleId = 0);
		new Task<TMetrics> ProcessSampleMetricsAsync(TGenome g, long sampleId = 0);
	}


}
