using Open.Collections;
using Open.Dataflow;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.Schemes
{
	public sealed class Kumite<TGenome> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{
		public Kumite(IGenomeFactory<TGenome> genomeFactory, ushort maximumLoss = ushort.MaxValue)
			: base(genomeFactory)
		{
			MaximumLoss = maximumLoss;
		}

		public readonly ushort MaximumLoss;

		readonly ConcurrentDictionary<IProblem<TGenome>, KumiteTournament<TGenome>> Hosts
			= new ConcurrentDictionary<IProblem<TGenome>, KumiteTournament<TGenome>>();

		readonly ConcurrentQueue<TGenome> PriorityContenders = new ConcurrentQueue<TGenome>();
		readonly ConcurrentQueue<TGenome> Breeders = new ConcurrentQueue<TGenome>();

		public override void AddProblems(IEnumerable<IProblem<TGenome>> problems)
		{
			foreach (var problem in problems)
			{
				var k = new KumiteTournament<TGenome>(problem, MaximumLoss);
				k.Subscribe(e =>
				{
					Announce((problem, e));
					Breeders.Enqueue(e.Genome);
				});
				Hosts.TryAdd(problem, k);
			}

			base.AddProblems(problems);
		}

		protected override void OnCancelled()
		{
			//throw new NotImplementedException();
		}

		void Post(TGenome genome)
		{
			foreach (var host in Hosts.Values)
			{
				host.Post(genome);
			}
		}

		protected override async Task StartInternal(CancellationToken token)
		{
			TGenome readyBreeder = null;
			while (!token.IsCancellationRequested)
			{
				if (PriorityContenders.TryDequeue(out TGenome g))
				{
					Post(g);
					continue;
				}

				if (Breeders.TryDequeue(out TGenome g2))
				{
					var mutate = Task.Run(() =>
					{
						if (Factory.AttemptNewMutation(g2, out TGenome mutation))
							PriorityContenders.Enqueue(mutation);
					});

					if (readyBreeder == null || readyBreeder.Hash == g2.Hash) readyBreeder = g2;
					else
					{
						var g1 = readyBreeder;
						readyBreeder = null;
						Factory
							.AttemptNewCrossover(g1, g2)?
							.ForEach(g3 => PriorityContenders.Enqueue(g3));
					}
					await mutate;
					continue;
				}

				Post(Factory.GenerateNew().First());
			}
		}
	}
}
