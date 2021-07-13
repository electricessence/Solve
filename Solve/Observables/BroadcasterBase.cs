using Open.Collections.Synchronized;
using Open.Disposable;
using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

namespace Solve
{
	// Important Note:
	// Tasks can build up and dominate the scheduler in ways that messages from a broadcast block won't make it out.
	// Using a synchronous announcer delegate will guarantee the announcements are recieved.

	public abstract class BroadcasterBase<T> : DisposableBase, IObservable<T>
	{
		readonly Subject<T> _subject = new();

		protected override void OnBeforeDispose()
			=> Complete();

		protected override void OnDispose()
			=> _subject.Dispose();

		T? _previous = default;
		internal void Broadcast(T message, bool uniqueOnly = false)
		{
			if (message is null) throw new ArgumentNullException(nameof(message));
			if (uniqueOnly && message.Equals(_previous)) return;
			_previous = message;
			_subject.OnNext(message);
		}

		protected void Complete()
			=> _subject.OnCompleted();

		protected void Fault(Exception exception)
			=> _subject.OnError(exception);

		public IDisposable Subscribe(IObserver<T> observer)
			=> AssertIsAlive(true) ? _subject.Subscribe(observer) : null!;
	}
}
