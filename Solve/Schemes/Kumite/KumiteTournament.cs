using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Solve.Schemes.Kumite
{
	public sealed class KumiteTournament<TGenome> : ProcessingSchemeBase<TGenome>
		where TGenome : class, IGenome
	{
		internal readonly IProblem<TGenome> Problem;
		internal KumiteLevel<TGenome> Root;
		internal ushort MaximumAllowedLosses;
		internal ITargetBlock<IGenomeFitness<TGenome, Fitness>> RejectProcessor;

		public KumiteTournament(IProblem<TGenome> problem, ushort maximumLoss = ushort.MaxValue, ITargetBlock<IGenomeFitness<TGenome, Fitness>> rejector = null) : base()
		{
			Problem = problem ?? throw new ArgumentNullException(nameof(problem));
			Root = new KumiteLevel<TGenome>(0, this);
			if (maximumLoss == 0) throw new ArgumentOutOfRangeException(nameof(maximumLoss), maximumLoss, "Must be greater than zero.");
			MaximumAllowedLosses = maximumLoss;
			RejectProcessor = rejector ?? DataflowBlock.NullTarget<IGenomeFitness<TGenome, Fitness>>();
		}

		protected override Task Process(IGenomeFitness<TGenome, Fitness> next)
			=> Root.Post(next);
	}
}
