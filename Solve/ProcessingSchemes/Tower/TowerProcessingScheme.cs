using Solve.Metrics;
using System;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	public sealed partial class TowerProcessingScheme<TGenome> : SynchronousProcessingSchemeBase<TGenome>
		where TGenome : class, IGenome
	{
		const ushort DEFAULT_CHAMPION_POOL_SIZE = 100;
		const ushort DEFAULT_MAX_LEVEL_LOSSES = 3;
		const ushort DEFAULT_MAX_LOSSES_BEFORE_ELIMINATION = DEFAULT_MAX_LEVEL_LOSSES * 30;

		internal readonly CounterCollection Counters;

		public TowerProcessingScheme(
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
			if (championPoolSize != 0)
				ChampionPool = new RankedPool<TGenome>(championPoolSize);

			this.Subscribe(e => Factory[0].EnqueueChampion(e.Genome));
		}

		public TowerProcessingScheme(
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
		Level Root = null;

		#region Champion Pool
		public readonly RankedPool<TGenome> ChampionPool;

		bool ProduceFromChampions()
		{
			if (ChampionPool == null) return false;

			var champions = ChampionPool.Ranked;
			var len = champions.Length;
			if (len > 0)
			{
				var top = champions[0];
				ReserveFactoryQueue.EnqueueForMutation(top);
				ReserveFactoryQueue.EnqueueForBreeding(top);

				var next = TriangularSelection.Descending.RandomOne(champions);
				ReserveFactoryQueue.EnqueueForMutation(next);
				ReserveFactoryQueue.EnqueueForBreeding(next);

				ReserveFactoryQueue.EnqueueForMutation(champions);
				ReserveFactoryQueue.EnqueueForBreeding(champions);

				return true;
			}

			return false;
		}
		#endregion

		protected override Task StartInternal(CancellationToken token)
		{
			Interlocked.CompareExchange(ref Root, new Level(0, this), null);

			return base.StartInternal(token);
		}

	}


}
