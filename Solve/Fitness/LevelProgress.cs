using Open.Disposable;
using System.Collections.Immutable;

namespace Solve;

public class LevelProgress<TGenome>(TGenome genome, ImmutableArray<Fitness> fitnesses) : DisposableBase
{
	public TGenome Genome { get; } = genome ?? throw new ArgumentNullException(nameof(genome));
	public ImmutableArray<Fitness> Fitnesses { get; } = fitnesses;
	public LossTracker Losses { get; } = new LossTracker();

	protected override void OnDispose() => Losses.Dispose();
}
