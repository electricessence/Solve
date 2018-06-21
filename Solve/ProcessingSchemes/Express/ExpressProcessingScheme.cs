using Solve.Metrics;
using System;
using System.Diagnostics.Contracts;

namespace Solve.ProcessingSchemes
{
	public sealed partial class ExpressProcessingScheme<TGenome> : ProcessingSchemeBase<TGenome, ExpressProcessingScheme<TGenome>.Tower>
		where TGenome : class, IGenome
	{
		const ushort DEFAULT_CHAMPION_POOL_SIZE = 100;
		const ushort DEFAULT_MAX_LEVEL_LOSSES = 3;
		const ushort DEFAULT_MAX_LOSSES_BEFORE_ELIMINATION = DEFAULT_MAX_LEVEL_LOSSES * 30;

		internal readonly CounterCollection Counters;

		public ExpressProcessingScheme(
			IGenomeFactory<TGenome> genomeFactory,
			(ushort First, ushort Minimum, ushort Step) poolSize,
			ushort maxLevelLosses = DEFAULT_MAX_LEVEL_LOSSES,
			ushort maxLossesBeforeElimination = DEFAULT_MAX_LOSSES_BEFORE_ELIMINATION,
			ushort championPoolSize = DEFAULT_CHAMPION_POOL_SIZE,
			CounterCollection counters = null)
			: base(genomeFactory)
		{
			if (poolSize.Minimum < 2)
				throw new ArgumentOutOfRangeException(nameof(poolSize), "Must be at least 2.");
			if (poolSize.First % 2 == 1)
				throw new ArgumentException("Must be a mutliple of 2.", nameof(poolSize.First));
			if (poolSize.Minimum % 2 == 1)
				throw new ArgumentException("Must be a mutliple of 2.", nameof(poolSize.Minimum));
			if (poolSize.Step % 2 == 1)
				throw new ArgumentException("Must be a mutliple of 2.", nameof(poolSize.Step));
			if (poolSize.First < poolSize.Minimum)
				throw new ArgumentException("Minumum must be less than or equal to First.", nameof(poolSize));
			Contract.EndContractBlock();

			PoolSize = poolSize;
			MaxLevelLosses = maxLevelLosses;
			MaxLossesBeforeElimination = maxLossesBeforeElimination;
			ChampionPoolSize = championPoolSize;
			Counters = counters;
			ReserveFactoryQueue = genomeFactory[2];
			ReserveFactoryQueue.ExternalProducers.Add(ProduceFromChampions);
		}

		public ExpressProcessingScheme(
			IGenomeFactory<TGenome> genomeFactory,
			ushort poolSize,
			ushort maxLevelLosses = DEFAULT_MAX_LEVEL_LOSSES,
			ushort maxLossesBeforeElimination = DEFAULT_MAX_LOSSES_BEFORE_ELIMINATION,
			ushort championPoolSize = DEFAULT_CHAMPION_POOL_SIZE,
			CounterCollection counters = null)
			: this(genomeFactory, (poolSize, poolSize, 2), maxLevelLosses, maxLossesBeforeElimination, championPoolSize, counters)
		{
		}

		// First, and Minimum allow for tapering of pool size as generations progress.
		public readonly (ushort First, ushort Minimum, ushort Step) PoolSize;
		public readonly ushort MaxLevelLosses;
		public readonly ushort MaxLossesBeforeElimination;
		public readonly ushort ChampionPoolSize;
		readonly IGenomeFactoryPriorityQueue<TGenome> ReserveFactoryQueue;

		bool ProduceFromChampions()
		{
			bool produced = false;
			foreach (var t in Towers.Values)
			{
				if (t.ProduceFromChampions(ReserveFactoryQueue))
					produced = true;
			}
			return produced;
		}
	}


}
