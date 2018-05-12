using Open.Dataflow;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

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

		public override void AddProblems(IEnumerable<IProblem<TGenome>> problems)
		{
			foreach (var problem in problems)
			{
				var k = new KumiteTournament<TGenome>(problem, MaximumLoss);
				var x = new ActionBlock<IGenomeFitness<TGenome, Fitness>>(
					e => Announce((problem, e)));
				k.LinkToWithExceptions(x);
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
				host.Post(genome).Wait();
			}
		}

		protected override Task StartInternal(CancellationToken token)
		{
			return Task.Run(cancellationToken: token, action: () =>
			{
				Parallel.ForEach(
					Factory.Generate(),
					newGenome =>
				{
					Post(newGenome);
					while (PriorityContenders.TryDequeue(out TGenome g))
						Post(g);
				});
			});
		}
	}
}
