using Solve.Metrics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Solve.ProcessingSchemes
{
	// ReSharper disable once PossibleInfiniteInheritance
	public abstract class TowerProcessingSchemeBase<TGenome> : PushProcessingSchemeBase<TGenome>
		where TGenome : class, IGenome
	{

		protected TowerProcessingSchemeBase(
			IGenomeFactory<TGenome> genomeFactory,
			SchemeConfig config,
			GenomeProgressionLog? genomeProgressionLog = null)
			: base(genomeFactory, genomeProgressionLog/*, true*/)
		{
			Config = config ?? throw new ArgumentNullException(nameof(config));
			Contract.EndContractBlock();

			ReserveFactoryQueue = genomeFactory[2];
			ReserveFactoryQueue.ExternalProducers.Add(ProduceFromChampions);
			this.Subscribe(e => Factory[0].EnqueueChampion(e.Update.Genome));
		}

		// First, and Minimum allow for tapering of pool size as generations progress.
		public SchemeConfig.Values Config { get; }
		readonly IGenomeFactoryPriorityQueue<TGenome> ReserveFactoryQueue;


		static readonly EqualityComparer<(TGenome Genome, Fitness Fitness)> EComparer
			= EqualityComparerUtility.Create<(TGenome Genome, Fitness Fitness)>(
				(a, b) => a.Genome.Hash == b.Genome.Hash,
				a => a.Genome.Hash.GetHashCode());

		static ImmutableArray<double> ScoreSelector((TGenome Genome, Fitness Fitness) gf)
			=> gf.Fitness.Results.Average;

		bool ProduceFromChampions()
			=> Problems
				.SelectMany(p => p.Pools, (p, r) => r.Champions)
				.Where(c => c != null && !c.IsEmpty)
				.Select(c =>
				{
					var champions = c.Ranked;
					var len = champions.Length;
					if (len <= 0) return false;

					var top = champions[0].Genome;
					ReserveFactoryQueue.EnqueueForMutation(top);
					ReserveFactoryQueue.EnqueueForBreeding(top);

					var next = TriangularSelection.Descending.RandomOne(champions).Genome;
					ReserveFactoryQueue.EnqueueForMutation(next);
					ReserveFactoryQueue.EnqueueForBreeding(next);

					foreach (var g in Pareto
						.Filter(champions, EComparer, ScoreSelector)
						.Select(gf => gf.Value.Genome))
					{
						ReserveFactoryQueue.EnqueueForBreeding(g);
					}

					//ReserveFactoryQueue.EnqueueForMutation(champions);
					//ReserveFactoryQueue.EnqueueForBreeding(champions);

					return true;

				})
				.ToArray()
				.Any();

	}
}
