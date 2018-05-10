using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Solve
{
	public abstract class ProcessingSchemeBase<TGenome>
		where TGenome : class, IGenome
	{
		protected readonly BroadcastBlock<IGenomeFitness<TGenome, Fitness>> ChampionAnnouncer;
		readonly ISourceBlock<IGenomeFitness<TGenome, Fitness>> _announcerBlock; // Cast once.

		protected ProcessingSchemeBase()
		{
			ChampionAnnouncer = new BroadcastBlock<IGenomeFitness<TGenome, Fitness>>(null);
			_announcerBlock = ChampionAnnouncer;
		}

		bool _isComplete = false;
		public Task Completion => ChampionAnnouncer.Completion;

		public abstract Task Post(IGenomeFitness<TGenome, Fitness> next);

		internal bool Announce(IGenomeFitness<TGenome, Fitness> newChampion)
			=> ChampionAnnouncer.Post(newChampion);

		public void Complete()
		{
			_isComplete = true;
			_announcerBlock.Complete();
		}

		public void Fault(Exception exception)
		{
			_isComplete = true;
			_announcerBlock.Fault(exception);
		}
	}
}
