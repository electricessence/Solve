using Solve.Metrics;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;

namespace Solve.ProcessingSchemes;

// ReSharper disable once PossibleInfiniteInheritance
public abstract class TowerSchemeBase<TGenome> : EnvironmentBase<TGenome>
	where TGenome : class, IGenome
{
	protected TowerSchemeBase(
		IGenomeFactory<TGenome> genomeFactory,
		SchemeConfig config,
		GenomeProgressionLog? genomeProgressionLog = null)
		: base(genomeFactory, genomeProgressionLog/*, true*/)
	{
		Config = config ?? throw new ArgumentNullException(nameof(config));
		Contract.EndContractBlock();

		ReserveFactoryQueue = genomeFactory[2];
		ReserveFactoryQueue.ExternalProducers.Add(ProduceFromChampions);
		this.Subscribe(e => Factory[0].EnqueueChampion(e.Genome));
	}

	// First, and Minimum allow for tapering of pool size as generations progress.
	public SchemeConfig.Values Config { get; }

	private readonly IGenomeFactoryPriorityQueue<TGenome> ReserveFactoryQueue;
	private static readonly EqualityComparer<(TGenome Genome, Fitness Fitness)> EComparer
		= EqualityComparerUtility.Create<(TGenome Genome, Fitness Fitness)>(
			(a, b) => a.Genome.Hash == b.Genome.Hash,
			a => a.Genome.Hash.GetHashCode(StringComparison.Ordinal));

	private static ImmutableArray<double> ScoreSelector((TGenome Genome, Fitness Fitness) gf)
		=> gf.Fitness.Results.Average;

	private bool ProduceFromChampions()
	{
		bool any = false;
		foreach (bool _ in Problems
			.SelectMany(p => p.Pools, (_, r) => r.Champions)
			.Where(c => c?.IsEmpty == false)
			.Select(c =>
			{
				ImmutableArray<(TGenome Genome, Fitness Fitness)> champions = c.Ranked;
				int len = champions.Length;
				if (len <= 0) return false;

				TGenome top = champions[0].Genome;
				ReserveFactoryQueue.EnqueueForMutation(top);
				ReserveFactoryQueue.EnqueueForBreeding(top);

				TGenome next = TriangularSelection.Descending.RandomOne(champions).Genome;
				ReserveFactoryQueue.EnqueueForMutation(next);
				ReserveFactoryQueue.EnqueueForBreeding(next);

				using Open.Collections.ArrayPoolSegment<((TGenome Genome, Fitness Fitness) Value, ImmutableArray<double> Score)> p
					= Pareto.Filter(champions, EComparer, ScoreSelector);

				foreach (((TGenome Genome, Fitness Fitness) value, _) in p.Segment)
				{
					ReserveFactoryQueue.EnqueueForBreeding(value.Genome);
				}

				//ReserveFactoryQueue.EnqueueForMutation(champions);
				//ReserveFactoryQueue.EnqueueForBreeding(champions);

				return true;
			}))
		{
			any = true;
		}

		return any;
	}
}
