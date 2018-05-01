using System;

namespace Solve.Schemes.Kumite
{
	public sealed class KumiteTournament<TGenome> : ProcessingSchemeBase<TGenome>
		where TGenome : class, IGenome
	{
		
		KumiteNode<TGenome> _node;

		public KumiteTournament(IProblem<TGenome> problem) :base()
		{
			_node = new KumiteNode<TGenome>(0, problem, ChampionAnnouncer);
		}

		protected override bool Process(GenomeFitness<TGenome> next)
			=> _node.Post(next);
	}
}
