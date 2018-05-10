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

		protected abstract Task Process(IGenomeFitness<TGenome, Fitness> next);

		internal bool Announce(IGenomeFitness<TGenome, Fitness> newChampion)
			=> ChampionAnnouncer.Post(newChampion);

		public DataflowMessageStatus OfferMessage(
			DataflowMessageHeader messageHeader,
			IGenomeFitness<TGenome, Fitness> messageValue,
			ISourceBlock<IGenomeFitness<TGenome, Fitness>> source,
			bool consumeToAccept)
		{
			if (_isComplete) return DataflowMessageStatus.DecliningPermanently;
			Process(messageValue).Wait();
			return DataflowMessageStatus.Accepted;
		}

		public IDisposable LinkTo(
			ITargetBlock<IGenomeFitness<TGenome, Fitness>> target,
			DataflowLinkOptions linkOptions)
			=> _announcerBlock.LinkTo(target, linkOptions);

		public IGenomeFitness<TGenome, Fitness> ConsumeMessage(
			DataflowMessageHeader messageHeader,
			ITargetBlock<IGenomeFitness<TGenome, Fitness>> target,
			out bool messageConsumed)
			=> _announcerBlock.ConsumeMessage(messageHeader, target, out messageConsumed);

		public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<IGenomeFitness<TGenome, Fitness>> target)
			=> _announcerBlock.ReserveMessage(messageHeader, target);

		public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<IGenomeFitness<TGenome, Fitness>> target)
			=> _announcerBlock.ReleaseReservation(messageHeader, target);

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
