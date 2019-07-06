using Open.Collections.Synchronized;
using Open.Disposable;
using System;
using System.Reactive.Linq;
using System.Threading;

namespace Solve
{
	// Important Note:
	// Tasks can build up and dominate the scheduler in ways that messages from a broadcast block won't make it out.
	// Using a synchronous announcer delegate will guarantee the announcements are recieved.

	public abstract class BroadcasterBase<T> : DisposableBase, IObservable<T>
	{
		readonly IObservable<T> _subscribable;
		ReadWriteSynchronizedLinkedList<IObserver<T>> _observers;

		protected override void OnBeforeDispose() => Complete();

		protected override void OnDispose() { }

		protected BroadcasterBase()
		{
			_observers = new ReadWriteSynchronizedLinkedList<IObserver<T>>();
			_subscribable = Observable.Create<T>(observer =>
			{
				_observers?.Add(observer);
				return () => _observers?.Remove(observer);
			});
		}

		T _previous;
		internal void Broadcast(T message, bool uniqueOnly = false)
		{
			if (uniqueOnly && message.Equals(_previous)) return;
			_previous = message;
			var observers = _observers;
			if (observers == null) return;
			foreach (var o in observers)
			{
				o.OnNext(message);
			}
		}

		protected void Complete()
		{
			var observers = Interlocked.Exchange(ref _observers, null);
			if (observers == null) return;
			using (observers)
			{
				foreach (var observer in observers)
					observer.OnCompleted();
			}
		}

		protected void Fault(in Exception exception)
		{
			var observers = Interlocked.Exchange(ref _observers, null);
			if (observers == null) return;
			using (observers)
			{
				foreach (var observer in observers)
					observer.OnError(exception);
			}
		}

		public IDisposable Subscribe(IObserver<T> observer)
			=> AssertIsAlive(true) ? _subscribable.Subscribe(observer) : null;
	}
}
