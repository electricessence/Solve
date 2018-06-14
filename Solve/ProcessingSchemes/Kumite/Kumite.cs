using Open.Disposable;
using Open.Threading;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	public sealed partial class KumiteProcessingScheme<TGenome>
		: ProcessingSchemeBase<TGenome, KumiteProcessingScheme<TGenome>.Tower>
		where TGenome : class, IGenome
	{
		public KumiteProcessingScheme(IGenomeFactory<TGenome> genomeFactory, ushort maximumLoss = ushort.MaxValue, ushort maxOffspring = ushort.MaxValue)
			: base(genomeFactory)
		{
			if (maximumLoss == 0) throw new ArgumentOutOfRangeException(nameof(maximumLoss), maximumLoss, "Must be greater than zero.");
			MaximumLoss = maximumLoss;
			MaxOffspring = maxOffspring;
		}

		public readonly ushort MaximumLoss;
		public readonly ushort MaxOffspring;

		readonly object BreederLock = new object();
		ConcurrentDictionary<TGenome, IFitness> Breeders = new ConcurrentDictionary<TGenome, IFitness>();


		const ushort MaximumBreederPoolSize = 100;

		static readonly OptimisticArrayObjectPool<ConcurrentDictionary<TGenome, IFitness>> BreedingPools
			= new OptimisticArrayObjectPool<ConcurrentDictionary<TGenome, IFitness>>(
				() => new ConcurrentDictionary<TGenome, IFitness>(), cd => cd.Clear());

		internal void EnqueueForBreeding(IGenomeFitness<TGenome> gf, bool addOnly = false)
		{
			bool returnAfterUpdate = addOnly || Breeders.IsEmpty;
			lock (BreederLock) Breeders[gf.Genome] = gf.Fitness.SnapShot();
			if (returnAfterUpdate) return;

			SelectFromBreedingPool();
		}

		Lazy<Task> SelectionTask;
		void SelectFromBreedingPool()
		{
			bool isOwned = false;
			var t = LazyInitializer.EnsureInitialized(ref SelectionTask, () => new Lazy<Task>(() =>
			{
				isOwned = true;
				return new Task(() =>
				{
					ConcurrentDictionary<TGenome, IFitness> breeders = null;
					if (ThreadSafety.TryLock(BreederLock,
						() => breeders = Interlocked.Exchange(ref Breeders, BreedingPools.Take())))
					{
						var breederList = breeders.ToArray();
						uint len = (uint)breederList.Length;
						if (len > 1)
						{
							breederList = breederList.Sort(true); // best fitness is near beginning (reversed order);
							if (len > MaximumBreederPoolSize)
							{
								breederList = breederList.Take(MaximumBreederPoolSize).ToArray();
								len = MaximumBreederPoolSize;
							}

							// Add the primary champion for sure.
							Factory[0].EnqueueForBreeding(breederList[0].Key);

							// Pick a random one.
							Factory[1].EnqueueForBreeding(TriangularSelection.Descending.RandomOne(breederList).Key);

							// Add pareto genomes.
							foreach (var p in GenomeFitness.Pareto(breederList))
								Factory[2].EnqueueForBreeding(p.Genome);
						}

						lock (BreederLock)
						{
							foreach (var e in breederList)
								Breeders.TryAdd(e.Key, e.Value);
						}
						BreedingPools.Give(breeders);
					}
				});
			}));

			if (isOwned)
			{
				try
				{
					t.Value.RunSynchronously();
				}
				finally
				{
					Interlocked.Exchange(ref SelectionTask, null);
				}
			}
		}

		async Task PostAsync(TGenome genome)
		{
			await Task.WhenAll(
				Towers.Values.Select(h => h.PostAsync(genome)));
		}


	}
}
