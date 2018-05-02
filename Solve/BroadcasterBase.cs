using Open.Dataflow;
using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Solve
{
	public abstract class BroadcasterBase<T> : ISourceBlock<T>
	{
		protected readonly ITargetBlock<T> Announcer;
		readonly ISourceBlock<T> _announcerBlock; // Cast once.

		protected BroadcasterBase()
		{
			var b = new BroadcastBlock<T>(null);
			Announcer = b.OnlyIfChanged(DataflowMessageStatus.Accepted);
			_announcerBlock = b;
		}

		bool _isComplete = false;
		public Task Completion => Announcer.Completion;

		protected bool Announce(T message)
			=> Announcer.Post(message);

		public IDisposable LinkTo(
			ITargetBlock<T> target,
			DataflowLinkOptions linkOptions)
			=> _announcerBlock.LinkTo(target, linkOptions);

		public T ConsumeMessage(
			DataflowMessageHeader messageHeader,
			ITargetBlock<T> target,
			out bool messageConsumed)
			=> _announcerBlock.ConsumeMessage(messageHeader, target, out messageConsumed);

		public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<T> target)
			=> _announcerBlock.ReserveMessage(messageHeader, target);

		public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<T> target)
			=> _announcerBlock.ReleaseReservation(messageHeader, target);

		public virtual void Complete()
		{
			_isComplete = true;
			_announcerBlock.Complete();
		}

		public virtual void Fault(Exception exception)
		{
			_isComplete = true;
			_announcerBlock.Fault(exception);
		}
	}
}
