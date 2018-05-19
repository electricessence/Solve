using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.Schemes
{
	public sealed class KingOfTheHill<TGenome> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{
		public KingOfTheHill(IGenomeFactory<TGenome> genomeFactory, ushort maxLevel = ushort.MaxValue, ushort maximumLoss = ushort.MaxValue)
			: base(genomeFactory)
		{
			MaximumLevel = maxLevel;
			MaximumLoss = maximumLoss;
		}

		public readonly ushort MaximumLevel;
		public readonly ushort MaximumLoss;

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

		Task QueueNewContenderAsync()
			=> Task.Run(QueueNewContender);

		void QueueNewContender()
			=> QueueNewContender(Factory.GenerateOne());

		internal void QueueNewContender(TGenome genome)
		{
			foreach (var host in Hosts.Values)
				host.QueueNewContender(genome);
		}

		internal void ProcessNewChampion(IGenomeFitness<TGenome, Fitness> champion)
		{
			var mutate = Task.Run(() =>
			{
				if (Factory.AttemptNewMutation(champion.Genome, out TGenome mutation))
					QueueNewContender(mutation);
			});

			//if (previous != null && champion.Genome.Hash != previous.Genome.Hash)
			//{
			//	var crossover = Task.Run(() =>
			//	{
			//		var crossovers = Factory.AttemptNewCrossover(champion.Genome, previous.Genome);
			//		if (crossovers != null) foreach (var child in crossovers)
			//				QueueNewContender(child);
			//	});
			//}
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
