/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Solve
{
	public interface IProblemPool<TGenome>
		 where TGenome : IGenome
	{
		IReadOnlyList<Metric> Metrics { get; }
		Func<TGenome, double[], FitnessContainer> Transform { get; }

		(TGenome Genome, FitnessContainer Fitness) BestFitness { get; }
		bool UpdateBestFitness(TGenome genome, FitnessContainer fitness);

		RankedPool<TGenome> Champions { get; }
	}

	/// <summary>
	/// Problems define what parameters need to be tested to resolve fitness.
	/// </summary>
	public interface IProblem<TGenome>
		 where TGenome : IGenome
	{
		int ID { get; }

		IReadOnlyList<IProblemPool<TGenome>> Pools { get; }

		IEnumerable<FitnessContainer> ProcessSample(TGenome g, long sampleId);
		Task<IEnumerable<FitnessContainer>> ProcessSampleAsync(TGenome g, long sampleId);

		long TestCount { get; }
	}

}
