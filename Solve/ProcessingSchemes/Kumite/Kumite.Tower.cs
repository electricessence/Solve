using System;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	public sealed partial class KumiteProcessingScheme<TGenome>
	{
		public sealed class Tower : GenomeFitnessBroadcasterBase<TGenome>, IGenomeProcessor<TGenome>
		{
			internal readonly KumiteProcessingScheme<TGenome> Environment;
			internal readonly IProblem<TGenome> Problem;
			internal readonly Level Root;
			internal readonly ushort MaximumAllowedLosses;
			internal readonly Action<IGenomeFitness<TGenome, Fitness>> Rejection;

			public Tower(IProblem<TGenome> problem,
				KumiteProcessingScheme<TGenome> environment,
				Action<IGenomeFitness<TGenome, Fitness>> rejectionProcessor = null) : base()
			{
				Problem = problem ?? throw new ArgumentNullException(nameof(problem));
				Environment = environment ?? throw new ArgumentNullException(nameof(environment));
				Root = new Level(0, this);
				MaximumAllowedLosses = environment.MaximumLoss;
				Rejection = rejectionProcessor ?? Nothing;
			}

			static void Nothing(IGenomeFitness<TGenome, Fitness> gf) { }

			public void Post(TGenome next)
				=> Root.Post(new GenomeFitness<TGenome, Fitness>(next, new Fitness()));

			public Task PostAsync(TGenome next)
				=> Root.PostAsync(new GenomeFitness<TGenome, Fitness>(next, new Fitness()));
		}

		protected override Tower NewTower(
			IProblem<TGenome> problem)
			=> new Tower(problem, this);

		protected override bool OnTowerBroadcast(
			Tower source,
			IGenomeFitness<TGenome, Fitness> genomeFitness)
		{
			EnqueueForBreeding(genomeFitness, true);
			return base.OnTowerBroadcast(source, genomeFitness);
		}
	}
}
