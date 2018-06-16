using System;
using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;

namespace Solve.ProcessingSchemes
{
	public sealed partial class ClassicProcessingScheme<TGenome> : ProcessingSchemeBase<TGenome, ClassicProcessingScheme<TGenome>.Tower>
		where TGenome : class, IGenome
	{
		public ClassicProcessingScheme(
			IGenomeFactory<TGenome> genomeFactory,
			(ushort First, ushort Minimum, ushort Step) poolSize,
			ushort maxLevelLosses = 5,
			ushort maxLossesBeforeElimination = 30,
			ushort reserveBreederPoolSize = 20)
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
			FactoryReserve = genomeFactory[2];
			FactoryReserve.ExternalProducers.Add(ProduceFromReserve);
			ReserveBreederPoolSize = reserveBreederPoolSize;
		}

		public ClassicProcessingScheme(
			IGenomeFactory<TGenome> genomeFactory,
			ushort poolSize,
			ushort maxLevelLosses = 5,
			ushort maxLossesBeforeElimination = 30,
			ushort reserveBreederPoolSize = 20)
			: this(genomeFactory, (poolSize, poolSize, 2), maxLevelLosses, maxLossesBeforeElimination, reserveBreederPoolSize)
		{
		}

		readonly IGenomeFactoryPriorityQueue<TGenome> FactoryReserve;

		// First, and Minimum allow for tapering of pool size as generations progress.
		public readonly (ushort First, ushort Minimum, ushort Step) PoolSize;
		public readonly ushort MaxLevelLosses;
		public readonly ushort MaxLossesBeforeElimination;
		public readonly ushort ReserveBreederPoolSize;

		readonly ConcurrentQueue<IGenomeFitness<TGenome, Fitness>> ReserveBreeders
			= new ConcurrentQueue<IGenomeFitness<TGenome, Fitness>>();

		Lazy<TGenome[]> ReadyReserve;

		void AddReserved(IGenomeFitness<TGenome, Fitness> genome)
		{
			ReserveBreeders.Enqueue(genome);
			if (ReadyReserve != null)
				Interlocked.Exchange(ref ReadyReserve, null);
			if (ReserveBreeders.Count > ReserveBreederPoolSize * 4)
				SortAndReturnLimited(); // Overflowing?
		}

		TGenome[] SortAndReturnLimited()
			=> LazyInitializer.EnsureInitialized(ref ReadyReserve, () => new Lazy<TGenome[]>(() =>
			{
				var result = ReserveBreeders.AsDequeueingEnumerable().Distinct().ToArray();
				result.Sort(true);
				var limited = result.Take(ReserveBreederPoolSize);
				foreach (var e in limited) ReserveBreeders.Enqueue(e);
				return limited.Select(g => g.Genome).ToArray();
			})).Value;

		bool ProduceFromReserve()
		{
			// Let it grow first...
			if (ReserveBreeders.Count < ReserveBreederPoolSize)
				return false;

			var breeders = SortAndReturnLimited();
			var len = breeders.Length;
			for (var i = 0; i < len; i++)
				FactoryReserve.Breed(breeders);

			return len != 0;
		}

		protected override bool OnTowerBroadcast(Tower source, IGenomeFitness<TGenome, Fitness> genomeFitness)
		{
			AddReserved(genomeFitness);
			return base.OnTowerBroadcast(source, genomeFitness);
		}
	}


}
