using Open.Collections;
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
					BreedChampion(e.Genome, e.Fitness.SampleCount);
				});
				Hosts.TryAdd(problem, k);
			}

			base.AddProblems(problems);
		}

		protected override void OnCancelled()
		{
			//throw new NotImplementedException();
		}


		TGenome _champion;
		void BreedChampion(TGenome champion, int offspringCount)
		{
			var old = Interlocked.Exchange(ref _champion, champion);
			if (old == null || old != champion && old.Hash != champion.Hash)
				Breed(champion, offspringCount, old);
		}

		public void Breed(TGenome contender, int offspringCount, TGenome champion = null)
		{
			if (contender == null) throw new ArgumentNullException(nameof(contender));
			Factory.EnqueueForExpansion(contender);

			if (champion == null) champion = _champion;
			if (champion != null && contender != champion && contender.Hash != champion.Hash)
			{
				// Breed 10.
				for (ushort i = 0; i < MaxOffspring && i < offspringCount; i++)
				{
					var x = Factory.AttemptNewCrossover(champion, contender);
					if (x == null) break;
					x.ForEach(g3 =>
					{
						Factory.EnqueueHighPriority(g3);
						foreach (var e in Factory.Expand(g3))
							Factory.EnqueueHighPriority(e);
					});
				}
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
