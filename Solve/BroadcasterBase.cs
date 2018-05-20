using Open.Dataflow;
using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Solve
{
	// Important Note:
	// Tasks can build up and dominate the scheduler in ways that messages from a broadcast block won't make it out.
	// Using a synchronous announcer delegate will guarantee the announcements are recieved.

	public abstract class BroadcasterBase<T> : ISourceBlock<T>
	{
		protected readonly Action<T> _synchronousAnnouncer;
		protected readonly ITargetBlock<T> Announcer;
		readonly ISourceBlock<T> _announcerBlock; // Cast once.

		protected BroadcasterBase(Action<T> announcer = null)
		{
			_synchronousAnnouncer = announcer;
			var b = new BroadcastBlock<T>(null);
			Announcer = b.OnlyIfChanged(DataflowMessageStatus.Accepted);
			_announcerBlock = b;
		}

		public Task Completion => Announcer.Completion;

		internal bool Announce(T message)
		{
			_synchronousAnnouncer?.Invoke(message);
			return Announcer.Post(message);
		}

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
			_announcerBlock.Complete();
		}

		public virtual void Fault(Exception exception)
		{
			_announcerBlock.Fault(exception);
		}
	}
}
