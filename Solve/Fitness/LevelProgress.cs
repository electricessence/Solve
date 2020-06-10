using System;
using System.Collections.Immutable;

namespace Solve
{
	public struct LevelProgress<TGenome>
	{
		public LevelProgress(TGenome genome, ImmutableArray<Fitness> fitnesses)
		{
			Genome = genome ?? throw new ArgumentNullException(nameof(genome));
			Fitnesses = fitnesses;
			Losses = new LossTracker();
		}

		public TGenome Genome { get; }
		public ImmutableArray<Fitness> Fitnesses { get; }
		public LossTracker Losses { get; }
	}
}
