using Open.Collections.Synchronized;
using System;
using System.Reactive.Linq;
using System.Threading;

namespace Solve
{
	// Important Note:
	// Tasks can build up and dominate the scheduler in ways that messages from a broadcast block won't make it out.
	// Using a synchronous announcer delegate will guarantee the announcements are recieved.

	public abstract class BroadcasterBase<T> : IObservable<T>
	{
		readonly IObservable<T> _subscribable;
		ReadWriteSynchronizedLinkedList<IObserver<T>> _observers;

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
		internal void Announce(T message, bool uniqueOnly = false)
		{
			if (!uniqueOnly || !message.Equals(_previous))
			{
				_previous = message;
				var observers = _observers;
				if (observers != null)
				{
					foreach (var o in observers)
					{
						o.OnNext(message);
					}
				}
			}

		}

		protected void Complete()
		{
			var observers = Interlocked.Exchange(ref _observers, null);
			if (observers != null)
			{
				using (observers)
				{
					foreach (var observer in observers)
						observer.OnCompleted();
				}
			}
		}

		protected void Fault(Exception exception)
		{
			var observers = Interlocked.Exchange(ref _observers, null);
			if (observers != null)
			{
				using (observers)
				{
					foreach (var observer in observers)
						observer.OnError(exception);
				}
			}
		}

		public IDisposable Subscribe(IObserver<T> observer)
		{
			return _subscribable.Subscribe(observer);
		}
	}
}
