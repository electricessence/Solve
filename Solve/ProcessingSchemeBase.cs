using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Solve
{
	public abstract class ProcessingSchemeBase<TGenome> : IProcessingScheme<TGenome>
		where TGenome : class, IGenome
	{
		protected readonly BroadcastBlock<GenomeFitness<TGenome>> ChampionAnnouncer;
		readonly ISourceBlock<GenomeFitness<TGenome>> _announcerBlock; // Cast once.

		protected ProcessingSchemeBase()
		{
			ChampionAnnouncer = new BroadcastBlock<GenomeFitness<TGenome>>(null);
			_announcerBlock = ChampionAnnouncer;
		}

		bool _isComplete = false;
		public Task Completion => ChampionAnnouncer.Completion;

		protected abstract bool Process(GenomeFitness<TGenome> next);

		protected bool Announce(GenomeFitness<TGenome> newChampion)
			=> ChampionAnnouncer.Post(newChampion);

		public DataflowMessageStatus OfferMessage(
			DataflowMessageHeader messageHeader,
			GenomeFitness<TGenome> messageValue,
			ISourceBlock<GenomeFitness<TGenome>> source,
			bool consumeToAccept)
		{
			if (_isComplete) return DataflowMessageStatus.DecliningPermanently;
			return Process(messageValue) ? DataflowMessageStatus.Accepted : DataflowMessageStatus.Declined;
		}

		public IDisposable LinkTo(
			ITargetBlock<GenomeFitness<TGenome>> target,
			DataflowLinkOptions linkOptions)
			=> _announcerBlock.LinkTo(target, linkOptions);

		public GenomeFitness<TGenome> ConsumeMessage(
			DataflowMessageHeader messageHeader,
			ITargetBlock<GenomeFitness<TGenome>> target,
			out bool messageConsumed)
			=> _announcerBlock.ConsumeMessage(messageHeader, target, out messageConsumed);

		public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<GenomeFitness<TGenome>> target)
			=> _announcerBlock.ReserveMessage(messageHeader, target);

		public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<GenomeFitness<TGenome>> target)
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
