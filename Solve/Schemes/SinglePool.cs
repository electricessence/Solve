﻿/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using Open.Collections;
using Open.Collections.Numeric;
using Open.Dataflow;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Solve.Schemes
{
	public class SinglePool<TGenome> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{
		readonly BroadcastBlock<(IProblem<TGenome> Problem, TGenome Genome)> TopGenome = new BroadcastBlock<(IProblem<TGenome> Problem, TGenome Genome)>(null);

		readonly ushort PoolSize;
		readonly ConcurrentDictionary<string, TGenome> Pool = new ConcurrentDictionary<string, TGenome>();

		public SinglePool(IGenomeFactory<TGenome> genomeFactory, ushort poolSize) : base(genomeFactory)
		{
			PoolSize = poolSize;
		}

		public override IObservable<(IProblem<TGenome> Problem, TGenome Genome)> AsObservable()
		{
			return TopGenome.AsObservable();
		}

		protected override Task StartInternal()
		{
			var TopGenomeFiltered = TopGenome.OnlyIfChanged(DataflowMessageStatus.Accepted);
			return Task.Run(() =>
			{

				while (true)
				{
					// Phase 1, make sure the pool is full.
					var addCount = PoolSize - Pool.Count;
					if (addCount > 0)
					{
						foreach (var newGenome in Factory.Generate().Take(addCount))
						{
							if (Pool.TryAdd(newGenome.Hash, newGenome))
							{
								foreach (var p in ProblemsInternal)
								{
									p.ProcessTest(newGenome, 0, true);
								}
							}
						}
					}

					var pCount = 0;
					var toRejectCount = new ConcurrentDictionary<string, int>();
					// Phase 2, process tests in order of sample count.
					foreach (var p in ProblemsInternal)
					{
						pCount++;
						var firstGroup = Pool.Select(kvp => p.GetFitnessFor(kvp.Value).Value)
							.GroupBy(f => f.Fitness.SampleCount)
							.OrderBy(g => g.Key).First()
							.OrderBy(g => g, GenomeFitness.Comparer<TGenome, Fitness>.Instance);

						foreach (var gf in firstGroup)
						{
							p.ProcessTest(gf.Genome, 0, true);
						}

						var fga = firstGroup.ToArray();
						if (fga.Length > 1)
						{
							foreach (var gf in fga.Skip(fga.Length / 2))
							{
								toRejectCount.AddValue(gf.Genome.Hash, 1);
							}
						}
					}

					var rejects = toRejectCount.Where(kvp => kvp.Value == pCount).Select(kvp => kvp.Key).ToArray();
					//foreach (var p in Problems)
					//{
					//	p.Reject(rejects);
					//}
					foreach (var reject in rejects)
					{
						Pool.TryRemove(reject);
					}

					foreach (var p in ProblemsInternal)
					{
						var top = Pool.Select(kvp => p.GetFitnessFor(kvp.Value).Value)
							.GroupBy(f => f.Fitness.SampleCount)
							.OrderByDescending(g => g.Key).First()
							.OrderBy(g => g, GenomeFitness.Comparer<TGenome, Fitness>.Instance).First().Genome;

						TopGenomeFiltered.Post((p, top));

						var ac = PoolSize - Pool.Count;
						if (ac > 0)
						{
							foreach (var newGenome in Factory.Expand(top).Take(ac))
							{
								var hash = newGenome.Hash;
								if (Pool.TryAdd(hash, newGenome))
								{
									p.ProcessTest(newGenome, 0, true);
								}
							}
						}

					}

				}
			});
		}
	}
}
