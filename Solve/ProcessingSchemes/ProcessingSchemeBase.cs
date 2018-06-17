using Open.Dataflow;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	public abstract class ProcessingSchemeBase<TGenome, TTower> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
		where TTower : GenomeFitnessBroadcasterBase<TGenome>, IGenomeProcessor<TGenome>
	{
		protected ProcessingSchemeBase(IGenomeFactory<TGenome> genomeFactory, ushort championPoolSize)
			: base(genomeFactory)
		{
			ChampionPoolSize = championPoolSize;
			if (championPoolSize != 0)
			{
				Champions = new ConcurrentQueue<IGenomeFitness<TGenome>>();
				FactoryReserve = genomeFactory[2];
				FactoryReserve.ExternalProducers.Add(ProduceFromChampions);
			}
		}

		public override void AddProblems(IEnumerable<IProblem<TGenome>> problems)
		{
			foreach (var problem in problems)
			{
				var k = NewTower(problem);
				k.Subscribe(e =>
				{
					if (OnTowerBroadcast(k, e))
						Broadcast((problem, e));
				});
				Towers.TryAdd(problem, k);
			}

			base.AddProblems(problems);
		}

		void Post(TGenome genome)
		{
			foreach (var host in Towers.Values)
				host.Post(genome);
		}

		protected override Task StartInternal(CancellationToken token)
			=> Task.Run(cancellationToken: token, action: () =>
			{
				Parallel.ForEach(Factory, new ParallelOptions
				{
					CancellationToken = token,
				}, Post);
			});

		#region Tower Managment
		protected readonly ConcurrentDictionary<IProblem<TGenome>, TTower> Towers
			= new ConcurrentDictionary<IProblem<TGenome>, TTower>();

		protected abstract TTower NewTower(IProblem<TGenome> problem);

		protected virtual bool OnTowerBroadcast(TTower source, IGenomeFitness<TGenome> genomeFitness)
		{
			// This includes 'variations' and at least 1 mutation.
			Factory[0].EnqueueChampion(genomeFitness.Genome);
			AddChampion(genomeFitness);
			return true;
		}
		#endregion

		#region Champion Pool

		public readonly ushort ChampionPoolSize;
		protected readonly IGenomeFactoryPriorityQueue<TGenome> FactoryReserve;
		readonly ConcurrentQueue<IGenomeFitness<TGenome>> Champions;
		Lazy<TGenome[]> RankedChampions;

		protected void AddChampion(IGenomeFitness<TGenome> genome)
		{
			Debug.Assert(genome != null);
			if (Champions == null || genome == null) return;

			Champions.Enqueue(genome);

			// Queue has changed.  Flush the last result.
			var rc = RankedChampions;
			if (rc != null) Interlocked.CompareExchange(ref RankedChampions, null, rc);

			var count = Champions.Count;
			if (count > ChampionPoolSize * 100)
			{
				Debug.WriteLine($"Champion pool size reached: {count}");
				GetRankedChampions(); // Overflowing?
			}
		}

		TGenome[] GetRankedChampions()
			=> LazyInitializer.EnsureInitialized(ref RankedChampions, () => new Lazy<TGenome[]>(() =>
			{
				var result = Champions
					// Drain queue (some*)
					.AsDequeueingEnumerable()
					// Eliminate duplicates
					.Distinct()
					// Have enough to work with? (*)
					.Take(ChampionPoolSize * 2)
					// Setup ordering. Need to use snapshots for comparison.
					.Select(e => (snapshot: e.Fitness.SnapShot(), genomeFitness: e))
					// Higher sample counts are more valuable as they only arrive here as champions.
					.OrderByDescending(e => e.snapshot.SampleCount)
					.ThenBy(e => e.snapshot)
					// Compile results.
					.Select(e => e.genomeFitness)
					.ToArray();

#if DEBUG
				((GenomeFactoryBase<TGenome>)Factory).MetricsCounter.Increment("Champion Ranking");
#endif

				// (.Take(ChampionPoolSize)) All champions deserve a chance, but we will only retain the ones that can fit in the pool.				
				foreach (var e in result.Take(ChampionPoolSize)) Champions.Enqueue(e);
				return result.Select(g => g.Genome).ToArray();
			})).Value;

		protected bool ProduceFromChampions()
		{
			if (Champions == null) return false;

			var champions = GetRankedChampions();
			var len = champions.Length;
			if (champions.Length > 0)
			{
				FactoryReserve.EnqueueForMutation(champions[0]);
				if (champions.Length > 1) FactoryReserve.Breed(champions);
#if DEBUG
				((GenomeFactoryBase<TGenome>)Factory).MetricsCounter.Increment("Champion Ranking Production");
#endif
				return true;
			}

			return false;
		}

		#endregion

	}
}
