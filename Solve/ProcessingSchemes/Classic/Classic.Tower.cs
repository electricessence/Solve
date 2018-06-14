using System;
using System.Threading.Tasks.Dataflow;

namespace Solve.ProcessingSchemes
{
	public sealed partial class ClassicProcessingScheme<TGenome>
	{
		public sealed class Tower : GenomeFitnessBroadcasterBase<TGenome>, IGenomeProcessor<TGenome>
		{
			internal readonly ClassicProcessingScheme<TGenome> Environment;
			internal readonly IProblem<TGenome> Problem;
			internal readonly Level Root;

			public Tower(IProblem<TGenome> problem,
				ClassicProcessingScheme<TGenome> environment,
				ITargetBlock<IGenomeFitness<TGenome, Fitness>> loserPool = null) : base()
			{
				Problem = problem ?? throw new ArgumentNullException(nameof(problem));
				Environment = environment ?? throw new ArgumentNullException(nameof(environment));
				Root = new Level(0, this);
			}

			public void Post(TGenome next)
				=> Root.Post(new GenomeFitness<TGenome, Fitness>(next, new Fitness()));

		}

		protected override Tower NewTower(IProblem<TGenome> problem)
			=> new Tower(problem, this);

		//protected override bool OnTowerBroadcast(Tower source, IGenomeFitness<TGenome, Fitness> genomeFitness)
		//{
		//	var factory = Factory[0];
		//	//factory.EnqueueVariations(e.Genome);
		//	//factory.EnqueueForMutation(e.Genome);
		//	factory.EnqueueForBreeding(genomeFitness.Genome, genomeFitness.Fitness.SampleCount / 2);
		//	return base.OnTowerBroadcast(source, genomeFitness);
		//}
	}

}
