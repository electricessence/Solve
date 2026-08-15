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

		// Champion-queue inflow (Factory[0].EnqueueChampion) used to also be driven
		// from here, via a broadcast subscription (`this.Subscribe(e =>
		// Factory[0].EnqueueChampion(e.Genome), ...)`) that fired once per tower
		// Broadcast. That duplicated TowerScheme.Level.cs's own direct call for the
		// very same climbing genomes (see the `won` block in
		// Level.ProcessContenderSafelyAsync for the full trace of both original event
		// sources this single remaining call site now covers), inflating breeding-
		// stock churn without changing which genomes counted as champions. Removed in
		// favor of that single call site — this class no longer needs to subscribe to
		// its own broadcast for champion inflow. (Other independent subscribers of
		// this same broadcast — dashboards, StagnationMonitor, tests — are unaffected;
		// Subject-based broadcast fans out to each subscriber separately.)
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
