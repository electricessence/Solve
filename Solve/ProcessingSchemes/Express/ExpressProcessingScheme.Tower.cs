using System;
using System.Threading.Tasks.Dataflow;

namespace Solve.ProcessingSchemes
{
	public sealed partial class ExpressProcessingScheme<TGenome>
	{
		public sealed class Tower : ProblemSpecificBroadcasterBase<TGenome>, IGenomeProcessor<TGenome>
		{
			internal readonly ExpressProcessingScheme<TGenome> Environment;
			internal readonly Func<TGenome, SampleFitnessCollectionBase> Problem;
			internal readonly Level Root;

			public Tower(
				Func<TGenome, SampleFitnessCollectionBase> problem,
				ExpressProcessingScheme<TGenome> environment,
				ushort championPoolSize,
				ITargetBlock<IGenomeFitness<TGenome, Fitness>> loserPool = null) : base(championPoolSize)
			{
				Problem = problem ?? throw new ArgumentNullException(nameof(problem));
				Environment = environment ?? throw new ArgumentNullException(nameof(environment));
				Root = new Level(0, this);
			}

			public void Post(TGenome next,
				bool express,
				bool expressToTop = false)
				=> Root.Post(
					(next, Problem(next), 0),
					express,
					expressToTop);

			public void Post(TGenome next)
				=> Post(next, false);

		}

		protected override Tower NewTower(Func<TGenome, SampleFitnessCollectionBase> problem)
			=> new Tower(problem, this, ChampionPoolSize);


		//protected override bool OnTowerBroadcast(Tower source, IGenomeFitness<TGenome> genomeFitness)
		//{
		//	//var genome = genomeFitness.Genome;
		//	//while (genome.RemainingVariations.ConcurrentTryMoveNext(out IGenome v))
		//	//{
		//	//	foreach (var host in Towers.Values)
		//	//	{
		//	//		host.Post(genome, true, host == source);
		//	//	}
		//	//}
		//	var factory = Factory[0];
		//	var genome = genomeFitness.Genome;
		//	var sampleCount = genomeFitness.Fitness.SampleCount;
		//	if (sampleCount > 100)
		//	{
		//		factory.Mutate(genome, sampleCount);
		//		factory.Breed(genome, sampleCount);
		//	}
		//	////factory.EnqueueVariations(e.Genome);
		//	////factory.EnqueueForMutation(e.Genome);
		//	//factory.EnqueueForBreeding(genome, genomeFitness.Fitness.SampleCount / 2);
		//	return base.OnTowerBroadcast(source, genomeFitness);
		//}
	}

}
