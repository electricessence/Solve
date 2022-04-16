using Open.Disposable;
using System;
using System.Reactive.Subjects;

namespace Solve;

public abstract class BroadcasterBase<T> : DisposableBase, IObservable<T>
{
	readonly Subject<T> _subject = new();

	protected bool HasObservers => _subject.HasObservers;

	protected override void OnBeforeDispose() => Complete();

	protected override void OnDispose() => _subject.Dispose();

	T? _previous = default;
	internal void Broadcast(T message)
	{
		if (message is null) throw new ArgumentNullException(nameof(message));
		_previous = message;
		_subject.OnNext(message);
	}

	internal void Broadcast(T message, bool uniqueOnly)
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
