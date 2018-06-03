/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using Open.Collections;
using Open.Collections.Numeric;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.Schemes
{
	public class SinglePool<TGenome> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{
		readonly ushort PoolSize;
		readonly ConcurrentDictionary<string, TGenome> Pool
			= new ConcurrentDictionary<string, TGenome>();

		public SinglePool(IGenomeFactory<TGenome> genomeFactory, ushort poolSize) : base(genomeFactory)
		{
			PoolSize = poolSize;
		}

		protected override void OnCancelled()
		{

		}

		void FillPool()
		{
			var addCount = PoolSize - Pool.Count;
			if (addCount > 0)
			{
				foreach (var newGenome in Factory.GenerateNew().Take(addCount))
				{
					if (Pool.TryAdd(newGenome.Hash, newGenome))
					{
						foreach (var p in ProblemsInternal)
						{
							p.ProcessTestAsync(newGenome, 0, true);
						}
					}
				}
			}
		}

		async Task ProcessAndReject()
		{
			var toRejectCount = new ConcurrentDictionary<string, int>();

			await Task.WhenAll(ProblemsInternal.Select(async p =>
			{
				var firstGroup = Pool
					.Select(kvp => p.GetFitnessFor(kvp.Value).Value)
					.GroupBy(f => f.Fitness.SampleCount)
					.OrderBy(g => g.Key).First();

				await Task.WhenAll(firstGroup.Select(gf => p.ProcessTestAsync(gf.Genome, 0, true)));

				var firstGroupResults = firstGroup
					.OrderBy(g => g, GenomeFitness.Comparer<TGenome, Fitness>.Instance)
					.ToArray();

				if (firstGroupResults.Length > 1)
				{
					foreach (var gf in firstGroupResults.Skip(firstGroupResults.Length / 2))
					{
						toRejectCount.AddValue(gf.Genome.Hash, 1);
					}
				}
			}));

			var pCount = ProblemsInternal.Count;
			var rejects = toRejectCount
				.Where(kvp => kvp.Value == pCount)
				.Select(kvp => kvp.Key)
				.ToArray();

			//foreach (var p in Problems)
			//{
			//	p.Reject(rejects);
			//}
			foreach (var reject in rejects)
			{
				Pool.TryRemove(reject);
			}
		}

		void EnqueueTopForExpansion()
		{
			foreach (var p in ProblemsInternal)
			{
				var top = Pool.Select(kvp => p.GetFitnessFor(kvp.Value).Value)
					.GroupBy(f => f.Fitness.SampleCount)
					.OrderByDescending(g => g.Key).First()
					.OrderBy(g => g, GenomeFitness.Comparer<TGenome, Fitness>.Instance).First();

				Broadcast((p, top));

				Factory.EnqueueForVariation(top.Genome);
			}
		}

		protected override async Task StartInternal(CancellationToken token)
		{

			while (!token.IsCancellationRequested)
			{
				// Phase 1, make sure the pool is full.
				FillPool();

				// Phase 2, process tests in order of sample count.
				await ProcessAndReject();

				// Phase 3, take the top gene and expand on it.
				EnqueueTopForExpansion();
			}
		}

	}
}
