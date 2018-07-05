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
		Func<TGenome, double[], Fitness> Transform { get; }

		(TGenome Genome, Fitness Fitness) BestFitness { get; }
		bool UpdateBestFitness(in TGenome genome, in Fitness fitness);

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

		IEnumerable<Fitness> ProcessSample(TGenome g, long sampleId);
		Task<IEnumerable<Fitness>> ProcessSampleAsync(TGenome g, long sampleId);

		long TestCount { get; }
	}

}
