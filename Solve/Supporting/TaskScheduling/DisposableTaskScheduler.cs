using System;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.Supporting.TaskScheduling;

public abstract class DisposableTaskScheduler : TaskScheduler, IDisposable
{
	/// <summary>Cancellation token used for disposal.</summary>
	protected readonly CancellationTokenSource DisposeCancellation = new();

	int _wasDisposed = 0;

	protected virtual void OnDispose() { }

	#region IDisposable Members

	public void Dispose()
	{
		if (_wasDisposed != 0
		|| Interlocked.CompareExchange(ref _wasDisposed, 1, 0) != 0)
		{
			return;
		}

		DisposeCancellation.Cancel();
		DisposeCancellation.Dispose();

		OnDispose();
	}

	#endregion

	/// <inheritdoc />
	protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
	{
		if (task is null) throw new ArgumentNullException(nameof(task));
		Contract.EndContractBlock();

		return !DisposeCancellation.Token.IsCancellationRequested
			&& (!taskWasPreviouslyQueued || TryDequeue(task))
			&& TryExecuteTask(task);
	}
}
