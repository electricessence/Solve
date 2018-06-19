using System;
using System.Diagnostics.Contracts;

namespace Solve.ProcessingSchemes
{
	public sealed partial class ClassicProcessingScheme<TGenome> : ProcessingSchemeBase<TGenome, ClassicProcessingScheme<TGenome>.Tower>
		where TGenome : class, IGenome
	{
		const ushort DEFAULT_CHAMPION_POOL_SIZE = 100;

		public ClassicProcessingScheme(
			IGenomeFactory<TGenome> genomeFactory,
			(ushort First, ushort Minimum, ushort Step) poolSize,
			ushort maxLevelLosses = 3,
			ushort maxLossesBeforeElimination = 3 * 20,
			ushort championPoolSize = DEFAULT_CHAMPION_POOL_SIZE)
			: base(genomeFactory, championPoolSize)
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
		}

		public ClassicProcessingScheme(
			IGenomeFactory<TGenome> genomeFactory,
			ushort poolSize,
			ushort maxLevelLosses = 5,
			ushort maxLossesBeforeElimination = 30,
			ushort championPoolSize = DEFAULT_CHAMPION_POOL_SIZE)
			: this(genomeFactory, (poolSize, poolSize, 2), maxLevelLosses, maxLossesBeforeElimination, championPoolSize)
		{
		}

		// First, and Minimum allow for tapering of pool size as generations progress.
		public readonly (ushort First, ushort Minimum, ushort Step) PoolSize;
		public readonly ushort MaxLevelLosses;
		public readonly ushort MaxLossesBeforeElimination;
	}


}
