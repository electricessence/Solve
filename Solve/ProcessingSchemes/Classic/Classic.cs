using System;
using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Solve.ProcessingSchemes
{
	public sealed partial class ClassicProcessingScheme<TGenome> : ProcessingSchemeBase<TGenome, ClassicProcessingScheme<TGenome>.Tower>
		where TGenome : class, IGenome
	{
		public ClassicProcessingScheme(
			IGenomeFactory<TGenome> genomeFactory,
			(ushort First, ushort Minimum, ushort Step) poolSize,
			ushort maxLevelLosses = 5,
			ushort maxLossesBeforeElimination = 30)
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
		}

		public ClassicProcessingScheme(
			IGenomeFactory<TGenome> genomeFactory,
			ushort poolSize,
			ushort maxLevelLosses = 5,
			ushort maxLossesBeforeElimination = 30)
			: this(genomeFactory, (poolSize, poolSize, 2), maxLevelLosses, maxLossesBeforeElimination)
		{
		}

		readonly IGenomeFactoryPriorityQueue<TGenome> FactoryReserve;

		// First, and Minimum allow for tapering of pool size as generations progress.
		public readonly (ushort First, ushort Minimum, ushort Step) PoolSize;
		public readonly ushort MaxLevelLosses;
		public readonly ushort MaxLossesBeforeElimination;

		readonly ConcurrentQueue<TGenome> ReserveBreeders
			= new ConcurrentQueue<TGenome>();

		void AddReserved(TGenome genome)
		{
			ReserveBreeders.Enqueue(genome);
			while (ReserveBreeders.Count > 100 && ReserveBreeders.TryDequeue(out TGenome discard)) { }
		}

		bool ProduceFromReserve()
		{
			// Dump and requeue...
			var all = ReserveBreeders.AsDequeueingEnumerable().Distinct().ToArray();
			foreach (var e in all) ReserveBreeders.Enqueue(e);

			if (all.Length > 1)
			{
				FactoryReserve.Breed(all); // Enqueuing can be problematic so we trigger breeding immediately...
				return true;
			}
			return false;
		}

		protected override bool OnTowerBroadcast(Tower source, IGenomeFitness<TGenome, Fitness> genomeFitness)
		{
			AddReserved(genomeFitness.Genome);
			return base.OnTowerBroadcast(source, genomeFitness);
		}
	}


}
