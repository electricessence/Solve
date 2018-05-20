using Open.Collections;
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
			Action<(IProblem<TGenome> Problem, IGenomeFitness<TGenome> GenomeFitness)> announcer,
			IGenomeFactory<TGenome> genomeFactory, ushort maxLevel = ushort.MaxValue, ushort minConvSamples = 20, ushort maximumLoss = ushort.MaxValue, ushort maxBreedingStock = 10)
			: base(genomeFactory, announcer)
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
				Hosts.TryAdd(problem, new KingOfTheHillTournament<TGenome>(QueueNewContenderAsync, problem, e =>
				{
					Announce((problem, e));
					ProcessNewChampion(e);
				}, MaximumLoss));
			}

			base.AddProblems(problems);
		}

		protected override void OnCancelled()
		{
			//throw new NotImplementedException();
		}

		async Task QueueNewContenderAsync()
		{
			var reader = Generated.Reader;
			if (reader.TryRead(out TGenome g))
				QueueNewContender(g);
			else
			{
				QueueNewContender(Factory.GenerateOne());
				await Generated.Writer.WriteAsync(Factory.GenerateOne());
			}
		}


		internal void QueueNewContender(TGenome genome)
		{
			foreach (var host in Hosts.Values)
				host.QueueNewContender(genome);
		}

		readonly ConcurrentQueue<IGenomeFitness<TGenome>> _unbredChampions = new ConcurrentQueue<IGenomeFitness<TGenome>>();
		readonly ConcurrentDictionary<TGenome, Fitness> _breeders = new ConcurrentDictionary<TGenome, Fitness>();

		internal void ProcessNewChampion(IGenomeFitness<TGenome, Fitness> champion)
		{
			if (champion.Fitness.HasConverged(MinimumConvergenceSamples))
				Cancel();
			//			_unbredChampions.Enqueue(champion);
			QueueChampion(champion);
		}

		void QueueChampion(IGenomeFitness<TGenome, Fitness> champion)
		{
			// Step 1: Get a sorted snapshot of the current breeding stock.
			var breedingStock = _breeders.ToArray().Sort();
			if (breedingStock.Length != 0)
			{
				// Step 2: Remove any excess lower performers.
				foreach (var toRemove in breedingStock.Skip(MaximumBreedingStock))
					_breeders.TryRemove(toRemove.Key, out Fitness f);

				// Step 3: Breed all the existing stock with the new champion.
				Factory.AttemptNewCrossover(champion.Genome, breedingStock.Select(c => c.Key).ToArray())?
					.ForEach(child => QueueNewContender(child));
			}

			// Step 4: Add the new champion to the stock.
			_breeders.TryAdd(champion.Genome, champion.Fitness);

			// NOTE: Since fitnesses can be changing on the fly, there is no need to retain a sorted order.

			// Step 5: Mutate the new champion.
			if (Factory.AttemptNewMutation(champion.Genome, out TGenome mutation))
				QueueNewContender(mutation);
		}

		async Task RunTournament(KingOfTheHillTournament<TGenome> tournament, CancellationToken token)
		{
			var a = tournament.NextChampion(MaximumLevel);
			//var b = tournament.NextChampion(MaximumLevel);
			//await b;
			await a;
		}

		protected override Task StartInternal(CancellationToken token)
			=> Task.WhenAll(Hosts.Values.Select(tournament => RunTournament(tournament, token)));

	}
}
