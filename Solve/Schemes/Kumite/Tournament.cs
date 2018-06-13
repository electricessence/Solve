using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Solve.Schemes
{
	public sealed class KumiteTournament<TGenome> : ProcessingSchemeBase<TGenome>
		where TGenome : class, IGenome
	{
		internal readonly Kumite<TGenome> Environment;
		internal readonly IProblem<TGenome> Problem;
		internal KumiteLevel<TGenome> Root;
		internal ushort MaximumAllowedLosses;
		internal ITargetBlock<IGenomeFitness<TGenome, Fitness>> LoserPool;

		public KumiteTournament(IProblem<TGenome> problem,
			Kumite<TGenome> environment,
			ITargetBlock<IGenomeFitness<TGenome, Fitness>> loserPool = null) : base()
		{
			Problem = problem ?? throw new ArgumentNullException(nameof(problem));
			Environment = environment ?? throw new ArgumentNullException(nameof(environment));
			Root = new KumiteLevel<TGenome>(0, this);
			MaximumAllowedLosses = environment.MaximumLoss;
			LoserPool = loserPool ?? DataflowBlock.NullTarget<IGenomeFitness<TGenome, Fitness>>();
		}

		public void Post(TGenome next)
			=> Root.Post(new GenomeFitness<TGenome, Fitness>(next, new Fitness()));

		public Task PostAsync(TGenome next)
			=> Root.PostAsync(new GenomeFitness<TGenome, Fitness>(next, new Fitness()));
	}
}
