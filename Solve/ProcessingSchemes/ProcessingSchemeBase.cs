using Open.Dataflow;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	public abstract class ProcessingSchemeBase<TGenome, TTower> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
		where TTower : IGenomeProcessor<TGenome>, IObservable<(TGenome Genome, SampleFitnessCollectionBase Fitness)>
	{
		protected ProcessingSchemeBase(IGenomeFactory<TGenome> genomeFactory)
			: base(genomeFactory)
		{
		}

		public void AddProblem(Func<TGenome, SampleFitnessCollectionBase> generator)
		{
			if (!CanStart)
				throw new InvalidOperationException("Cannot add new problems after already starting.");

			var k = NewTower(generator);
			k.Subscribe(e =>
			{
				if (OnTowerBroadcast(k, e))
					Broadcast(e);
			});
			Towers.Add(k);
		}

		protected void Post(TGenome genome)
		{
			foreach (var host in Towers)
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
		protected readonly List<TTower> Towers
			= new List<TTower>();

		protected abstract TTower NewTower(Func<TGenome, SampleFitnessCollectionBase> generator);

		protected virtual bool OnTowerBroadcast(TTower source, (TGenome Genome, SampleFitnessCollectionBase Fitness) genomeFitness)
		{
			// This includes 'variations' and at least 1 mutation.
			Factory[0].EnqueueChampion(genomeFitness.Genome);
			return true;
		}
		#endregion

	}
}
