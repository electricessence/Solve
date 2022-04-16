using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes;

public interface ITower<TGenome> :
	IObservable<(TGenome Genome, Fitness, IProblem<TGenome> Problem, int PoolIndex)>
	where TGenome : class, IGenome
{
	SchemeConfig.Values Config { get; }
	IProblem<TGenome> Problem { get; }
	IGenomeFactory<TGenome> Factory { get; }

	ValueTask PostAsync(TGenome next);

	public ImmutableArray<Fitness> NewFitness()
		=> Problem.Pools.Select(f => new Fitness(f.Metrics)).ToImmutableArray();
}
