using Open.Arithmetic;
using Open.Dataflow;
using Open.Disposable;
using Open.Numeric;
using Open.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.Schemes
{
	public sealed class Kumite<TGenome> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{
		public Kumite(IGenomeFactory<TGenome> genomeFactory, ushort maximumLoss = ushort.MaxValue, ushort maxOffspring = ushort.MaxValue)
			: base(genomeFactory)
		{
			if (maximumLoss == 0) throw new ArgumentOutOfRangeException(nameof(maximumLoss), maximumLoss, "Must be greater than zero.");
			MaximumLoss = maximumLoss;
			MaxOffspring = maxOffspring;
		}

		public readonly ushort MaximumLoss;
		public readonly ushort MaxOffspring;

		readonly ConcurrentDictionary<IProblem<TGenome>, KumiteTournament<TGenome>> Hosts
			= new ConcurrentDictionary<IProblem<TGenome>, KumiteTournament<TGenome>>();

		readonly object BreederLock = new object();
		ConcurrentDictionary<TGenome, IFitness> Breeders = new ConcurrentDictionary<TGenome, IFitness>();

		public override void AddProblems(IEnumerable<IProblem<TGenome>> problems)
		{
			foreach (var problem in problems)
			{
				var k = new KumiteTournament<TGenome>(problem, this);
				k.Subscribe(e =>
				{
					Broadcast((problem, e));
					var genome = e.Genome;
					EnqueueForBreeding(e, true);
					Factory.EnqueueForProcessing(genome);
				});
				Hosts.TryAdd(problem, k);
			}

			base.AddProblems(problems);
		}

		const ushort MaximumBreederPoolSize = 100;

		static readonly OptimisticArrayObjectPool<ConcurrentDictionary<TGenome, IFitness>> BreedingPools
			= new OptimisticArrayObjectPool<ConcurrentDictionary<TGenome, IFitness>>(
				() => new ConcurrentDictionary<TGenome, IFitness>(), cd => cd.Clear());

		internal void EnqueueForBreeding(IGenomeFitness<TGenome> gf, bool addOnly = false)
		{
			if (gf.Fitness.SampleCount < 100) return;
			bool returnAfterUpdate = addOnly || Breeders.IsEmpty;
			lock (BreederLock) Breeders[gf.Genome] = gf.Fitness.SnapShot();
			if (returnAfterUpdate) return;

			SelectFromBreedingPool();
		}

		void SelectFromBreedingPool()
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
						len = 100;
					}
					 
					// Add the primary champion for sure.
					Factory.EnqueueForProcessing(breederList[0].Key);

					// Pick a random one.
					Factory.EnqueueForProcessing(TriangularSelection.Descending.RandomOne(breederList).Key);

					//// Add pareto genomes.
					//foreach (var p in GenomeFitness.Pareto(breederList))
					//	Factory.EnqueueForProcessing(p.Genome);
				}

				lock (BreederLock)
				{
					foreach (var e in breederList)
						Breeders.TryAdd(e.Key, e.Value);
				}
				BreedingPools.Give(breeders);
			}
		}


		void Post(TGenome genome)
		{
			foreach (var host in Hosts.Values)
				host.Post(genome);
		}

		async Task PostAsync(TGenome genome)
		{
			await Task.WhenAll(
				Hosts.Values.Select(h => h.PostAsync(genome)));
		}

		protected override Task StartInternal(CancellationToken token)
			=> Task.Run(cancellationToken: token, action: () =>
			{
				//foreach (var g in Factory.AsParallel())
				//	PostAsync(g).ConfigureAwait(false);

				//var pc = Environment.ProcessorCount;
				//Console.WriteLine("PROCESSOR COUNT: {0}", pc);
				Parallel.ForEach(Factory, new ParallelOptions
				{
					CancellationToken = token,
					//MaxDegreeOfParallelism = 2 * pc - 1
				}, Post);

				//Parallel.ForEach(Factory, new ParallelOptions
				//{
				//	CancellationToken = token,
				//}, Post);
			});

	}
}
