using Open.Collections.Synchronized;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Solve.Schemes
{
	public sealed class KingOfTheHill<TGenome> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{
		public KingOfTheHill(
			IGenomeFactory<TGenome> genomeFactory, ushort maxLevel = ushort.MaxValue, ushort minConvSamples = 20, ushort maximumLoss = ushort.MaxValue, ushort maxBreedingStock = 10)
			: base(genomeFactory)
		{
			MaximumLevel = maxLevel;
			MinimumConvergenceSamples = minConvSamples;
			MaximumLoss = maximumLoss;
			MaximumBreedingStock = maxBreedingStock;
		}

		readonly Channel<TGenome> Generated = Channel.CreateBounded<TGenome>(50);

		public readonly ushort MaximumBreedingStock;
		public readonly ushort MaximumLevel;
		public readonly ushort MaximumLoss;
		public readonly ushort MinimumConvergenceSamples;

		readonly ConcurrentDictionary<IProblem<TGenome>, KingOfTheHillTournament<TGenome>> Hosts
			= new ConcurrentDictionary<IProblem<TGenome>, KingOfTheHillTournament<TGenome>>();

		public override void AddProblems(IEnumerable<IProblem<TGenome>> problems)
		{
			foreach (var problem in problems)
			{
				var t = new KingOfTheHillTournament<TGenome>(QueueNewContenderAsync, problem, MaximumLoss);
				t.Subscribe(e =>
				{
					Broadcast((problem, e));
					ProcessNewChampion(e);
				});
				Hosts.TryAdd(problem, t);
			}

			base.AddProblems(problems);
		}

		protected override void OnCancelled()
		{
			//throw new NotImplementedException();
		}

		async Task QueueNewContenderAsync()
		{
			if (!_unbredChampions.TryDequeue(out IGenomeFitness<TGenome> champion) || !QueueChampion(champion))
			{
				var reader = Generated.Reader;
				if (reader.TryRead(out TGenome g))
					QueueNewContender(g);
				else
				{
					var newGenomes = Factory.GenerateNew();
					QueueNewContender(newGenomes.First());
					await Generated.Writer.WriteAsync(newGenomes.First());
				}
			}
		}


		internal void QueueNewContender(TGenome genome)
		{
#if DEBUG
			if (!_seen.Add(genome.Hash))
			{
				Console.WriteLine("Already Seen: {0}", genome.Hash);
			}
#endif

			foreach (var host in Hosts.Values)
				host.QueueNewContender(genome);
		}


		void QueueNewContender(TGenome genome, ref bool queued)
		{
			QueueNewContender(genome);
			queued = true;
		}

#if DEBUG
		readonly LockSynchronizedHashSet<string> _seen = new LockSynchronizedHashSet<string>();
#endif
		readonly ConcurrentQueue<IGenomeFitness<TGenome>> _unbredChampions = new ConcurrentQueue<IGenomeFitness<TGenome>>();
		readonly ConcurrentDictionary<TGenome, IFitness> _breeders = new ConcurrentDictionary<TGenome, IFitness>();

		internal void ProcessNewChampion(IGenomeFitness<TGenome> champion)
		{
			if (champion.Fitness.HasConverged(MinimumConvergenceSamples))
				Cancel();
			_unbredChampions.Enqueue(champion);
		}

		bool QueueChampion(IGenomeFitness<TGenome> champion)
		{
			var queued = false;
			// Step 1: Get a sorted snapshot of the current breeding stock.
			var breedingStock = _breeders.ToArray().Sort();
			if (breedingStock.Length != 0)
			{
				// Step 2: Remove any excess lower performers.
				foreach (var toRemove in breedingStock.Skip(MaximumBreedingStock))
					_breeders.TryRemove(toRemove.Key, out IFitness f);

				// Step 3: Breed all the existing stock with the new champion.
				foreach (var child in Factory.AttemptNewCrossover(champion.Genome, breedingStock.Select(c => c.Key).ToArray()) ?? Enumerable.Empty<TGenome>())
				{
					QueueNewContender(child, ref queued);
				}
			}

			// Step 4: Add the new champion to the stock.
			_breeders.TryAdd(champion.Genome, champion.Fitness);

			// NOTE: Since fitnesses can be changing on the fly, there is no need to retain a sorted order.

			// Step 5: Mutate the new champion.
			foreach (var expansion in Factory.Expand(champion.Genome))
				QueueNewContender(expansion, ref queued);

			return queued;
		}

		async Task RunTournament(KingOfTheHillTournament<TGenome> tournament, CancellationToken token)
		{
			//IGenomeFitness<TGenome, Fitness> contender = null;
			//for (uint i = 0; i < MaximumLevel; i++)
			//{
			//	contender = await tournament.NextChampion(i, contender);
			//}
			await tournament.NextChampion(MaximumLevel);
		}

		protected override Task StartInternal(CancellationToken token)
			=> Task.WhenAll(Hosts.Values.Select(tournament => RunTournament(tournament, token)));

	}
}
