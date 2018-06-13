using System;
using System.Threading.Tasks.Dataflow;

namespace Solve.Schemes
{
	public sealed partial class Classic<TGenome>
	{
		sealed class Tournament : ProcessingSchemeBase<TGenome>
		{
			public readonly Classic<TGenome> Environment;
			public readonly IProblem<TGenome> Problem;
			public readonly Level Root;

			public Tournament(IProblem<TGenome> problem,
				Classic<TGenome> environment,
				ITargetBlock<IGenomeFitness<TGenome, Fitness>> loserPool = null) : base()
			{
				Problem = problem ?? throw new ArgumentNullException(nameof(problem));
				Environment = environment ?? throw new ArgumentNullException(nameof(environment));
				Root = new Level(0, this);
			}

			public void Post(TGenome next)
				=> Root.Post(new GenomeFitness<TGenome, Fitness>(next, new Fitness()));

		}
	}

}
