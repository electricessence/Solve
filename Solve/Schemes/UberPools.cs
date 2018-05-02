/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using Open.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Solve.Schemes
{

	public sealed class UberPools<TGenome> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{
		readonly ushort PoolSize;
		public readonly ushort MinSampleCount;

		public UberPools(IGenomeFactory<TGenome> genomeFactory, ushort poolSize, ushort minSampleCount = 10) : base(genomeFactory)
		{
			MinSampleCount = minSampleCount;
			PoolSize = poolSize;
		}


		protected override Task StartInternal()
		{
			throw new NotImplementedException();
		}

		async Task ProcessContenderOnce(
			(IProblem<TGenome> Problem, Fitness Fitness)[] results,
			TGenome genome,
			long sampleId = 0
		)
		{
			var r = await ProblemsInternal.ProcessOnce(genome, sampleId);
			if (results.Length != r.Length)
				throw new Exception("Problem added/removed while processing.");
			for (var f = 0; f < r.Length; f++)
			{
				var result = r[f];
				var data = results[f];
				if (result.Problem != data.Problem)
					throw new Exception("Problem changed while processing.");
				data.Fitness.Merge(result.Fitness);
			}
		}

		async Task<(TGenome Genome, (IProblem<TGenome> Problem, Fitness Fitness)[])?> TryGetContender(
			IEnumerator<TGenome> source,
			int samples,
			bool useGlobalFitness = false)
		{
			var mid = samples / 2;

			(IProblem<TGenome> Problem, Fitness Fitness)[] results = null;

			TGenome genome;
			while (source.ConcurrentTryMoveNext(out genome)) // Using a loop instead of recursion.
			{
				results = ProblemsInternal.Select(p => (p, new Fitness())).ToArray();

				for (var i = 0; i < samples; i++)
				{
					await ProcessContenderOnce(results, genome, useGlobalFitness ? 0 : (-i));

					// Look for lemons and reject them early.
					if (i > mid && results.Any(s => s.Fitness.Scores[0] < 0))
					{
						genome = null;
						break; // Try again...
					}
				}
				if (genome != null) break;
			}

			if (genome == null) return null;

			return (genome, results);
		}

		(IProblem<TGenome> Problem, GenomeFitness<TGenome> Fitness)[] NextContender(
			IEnumerable<(TGenome Genome, (IProblem<TGenome> Problem, Fitness Fitness)[] Results)> pool)
		{
			// Transform...
			return pool
				.SelectMany(e => e.Results.Select(v => (Problem: v.Problem, Fitness: new GenomeFitness<TGenome>(e.Genome, v.Fitness))))
				.GroupBy(g => g.Problem)
				.Select(e => e.OrderBy(f => f.Fitness, GenomeFitness.Comparer<TGenome>.Instance).First())
				.ToArray();
		}

		async Task<(IProblem<TGenome> Problem, GenomeFitness<TGenome> Fitness)[]> NextContender(
			bool useGlobalFitness = false)
		{
			var e = Factory.Generate().GetEnumerator();
			return NextContender(
				await Task.WhenAll(
					Enumerable.Range(0, PoolSize)
						.Select(
							i => TryGetContender(e, MinSampleCount, useGlobalFitness).ContinueWith(t =>
							{
								var next = t.Result;
								if (!next.HasValue) throw new Exception("No more genomes?");
								return next.Value;
							})
						)
					)
				);
		}

		//async Task<KeyValuePair<IProblem<TGenome>, GenomeFitness<TGenome>>[]> NextCondendingVariation(
		//	TGenome genome,
		//	bool includeOriginal = false)
		//{
		//	var results = new List<KeyValuePair<TGenome, KeyValuePair<IProblem<TGenome>, Fitness>[]>>();
		//	var variations = ((IEnumerable<TGenome>)(genome.Variations));
		//	if (includeOriginal) variations = (new TGenome[] { genome }).Concat(variations);
		//	var e = variations.GetEnumerator();

		//	while (true)
		//	{
		//		var next = await TryGetContender(e, MinSampleCount);
		//		if (next.HasValue) results.Add(next.Value);
		//		else break;
		//	}
		//	return NextContender(results);
		//}

	}


}