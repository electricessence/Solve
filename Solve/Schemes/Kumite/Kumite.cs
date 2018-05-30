using Open.Dataflow;
using System;
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

		readonly ConcurrentQueue<TGenome> Breeders = new ConcurrentQueue<TGenome>();

		public override void AddProblems(IEnumerable<IProblem<TGenome>> problems)
		{
			foreach (var problem in problems)
			{
				var k = new KumiteTournament<TGenome>(problem, this);
				k.Subscribe(e =>
				{
					Broadcast((problem, e));
					Factory.EnqueueForExpansion(e.Genome);
					Factory.Breed(e.Genome);
				});
				Hosts.TryAdd(problem, k);
			}

			base.AddProblems(problems);
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

				var pc = Environment.ProcessorCount;
				//Console.WriteLine("PROCESSOR COUNT: {0}", pc);
				Parallel.ForEach(Factory, new ParallelOptions
				{
					CancellationToken = token,
					MaxDegreeOfParallelism = 2 * pc - 1
				}, Post);

				//Parallel.ForEach(Factory, new ParallelOptions
				//{
				//	CancellationToken = token,
				//}, Post);
			});

	}
}
