using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
#if DEBUG
	// ReSharper disable once RedundantUsingDirective
	using System.Diagnostics;
#endif

	// ReSharper disable once PossibleInfiniteInheritance
	public sealed partial class TowerProcessingScheme<TGenome> : ProcessingSchemeBase<TGenome>
		where TGenome : class, IGenome
	{
		const ushort DEFAULT_MAX_LEVEL_LOSSES = 3;
		const ushort DEFAULT_MAX_LOSSES_BEFORE_ELIMINATION = DEFAULT_MAX_LEVEL_LOSSES * 30;

		public TowerProcessingScheme(
			IGenomeFactory<TGenome> genomeFactory,
			in (ushort First, ushort Minimum, ushort Step) poolSize,
			ushort maxLevels = ushort.MaxValue,
			ushort maxLevelLosses = DEFAULT_MAX_LEVEL_LOSSES,
			ushort maxLossesBeforeElimination = DEFAULT_MAX_LOSSES_BEFORE_ELIMINATION)
			: base(genomeFactory/*, true*/)
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
			MaxLevels = maxLevels;
			MaxLevelLosses = maxLevelLosses;
			MaxLossesBeforeElimination = maxLossesBeforeElimination;
			ReserveFactoryQueue = genomeFactory[2];
			ReserveFactoryQueue.ExternalProducers.Add(ProduceFromChampions);
			this.Subscribe(e => Factory[0].EnqueueChampion(e.GenomeFitness.Genome));
		}

		public TowerProcessingScheme(
			IGenomeFactory<TGenome> genomeFactory,
			ushort poolSize,
			ushort maxLevels = ushort.MaxValue,
			ushort maxLevelLosses = DEFAULT_MAX_LEVEL_LOSSES,
			ushort maxLossesBeforeElimination = DEFAULT_MAX_LOSSES_BEFORE_ELIMINATION)
			: this(genomeFactory, (poolSize, poolSize, 2), maxLevels, maxLevelLosses, maxLossesBeforeElimination) { }

		// First, and Minimum allow for tapering of pool size as generations progress.
		public readonly (ushort First, ushort Minimum, ushort Step) PoolSize;
		public readonly ushort MaxLevels;
		public readonly ushort MaxLevelLosses;
		public readonly ushort MaxLossesBeforeElimination;
		readonly IGenomeFactoryPriorityQueue<TGenome> ReserveFactoryQueue;

		static readonly EqualityComparer<(TGenome Genome, Fitness Fitness)> EComparer
			= EqualityComparerUtility.Create<(TGenome Genome, Fitness Fitness)>(
				(a, b) => a.Genome.Hash == b.Genome.Hash,
				a => a.Genome.Hash.GetHashCode());

		static ReadOnlyMemory<double> ScoreSelector((TGenome Genome, Fitness Fitness) gf)
			=> gf.Fitness.Results.Average;

		bool ProduceFromChampions()
			=> Problems
				.SelectMany(p => p.Pools, (p, r) => r.Champions)
				.Where(c => c != null && !c.IsEmpty)
				.Select(c =>
				{
					var champions = c.Ranked;
					var len = champions.Length;
					if (len <= 0) return false;

					var top = champions[0].Genome;
					ReserveFactoryQueue.EnqueueForMutation(top);
					ReserveFactoryQueue.EnqueueForBreeding(top);

					var next = TriangularSelection.Descending.RandomOne(champions).Genome;
					ReserveFactoryQueue.EnqueueForMutation(next);
					ReserveFactoryQueue.EnqueueForBreeding(next);

					foreach (var g in Pareto
						.Filter(champions, EComparer, ScoreSelector)
						.Select(gf => gf.Value.Genome))
					{
						ReserveFactoryQueue.EnqueueForBreeding(g);
					}

					//ReserveFactoryQueue.EnqueueForMutation(champions);
					//ReserveFactoryQueue.EnqueueForBreeding(champions);

					return true;

				})
				.ToArray()
				.Any();

		//IReadOnlyList<ProblemTower> Towers;
		private IEnumerable<ProblemTower> ActiveTowers;

		protected override Task StartInternal(CancellationToken token)
		{
			var towers = Problems.Select(p => new ProblemTower(p, this)).ToList();
			//Towers = towers.AsReadOnly();
			ActiveTowers = towers.Where(t => !t.Problem.HasConverged);
			return base.StartInternal(token);
		}

#if DEBUG
		// ReSharper disable once UnusedMember.Local
		static StringBuilder GetGenomeInfo(TGenome genome)
		{
			var sb = new StringBuilder(genome.Hash);
			sb.AppendLine();
			foreach (var logEntry in genome.Log)
			{
				sb.Append(logEntry.Category)
					.Append(" > ")
					.Append(logEntry.Message);

				var data = logEntry.Data;
				if (!string.IsNullOrWhiteSpace(data))
					sb.Append(':').AppendLine().Append(data);

				sb.AppendLine();
			}
			return sb;
		}
#endif

		protected override Task PostAsync(TGenome genome, CancellationToken token)
		{
			if (genome == null) throw new ArgumentNullException(nameof(genome));
			Contract.EndContractBlock();

#if DEBUG
			//Debug.WriteLine(
			//	$"Posting (async):\n{GetGenomeInfo(genome)}",
			//	"TowerProcessingScheme");
#endif
			return Task.WhenAll(ActiveTowers
				.Select(t => t.PostAsync(genome, token)));
		}

		protected override void Post(TGenome genome)
		{
			if (genome == null) throw new ArgumentNullException(nameof(genome));
			Contract.EndContractBlock();

#if DEBUG
			//Debug.WriteLine(
			//	$"Posting:\n{GetGenomeInfo(genome)}\n",
			//	"TowerProcessingScheme");
#endif

			foreach (var t in ActiveTowers)
				t.Post(genome);
		}
	}


}
