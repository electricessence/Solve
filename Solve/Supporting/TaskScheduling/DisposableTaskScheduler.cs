using System.Diagnostics.Contracts;

namespace Solve.Supporting.TaskScheduling;

#pragma warning disable IDE0079 // Remove unnecessary suppression
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1063:Implement IDisposable Correctly", Justification = "<Pending>")]
#pragma warning restore IDE0079 // Remove unnecessary suppression
public abstract class DisposableTaskScheduler : TaskScheduler, IDisposable
{
	/// <summary>Cancellation token used for disposal.</summary>
	protected readonly CancellationTokenSource DisposeCancellation = new();
	private int _wasDisposed;

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
		ArgumentNullException.ThrowIfNull(task);
		Contract.EndContractBlock();

		return !DisposeCancellation.Token.IsCancellationRequested
			&& (!taskWasPreviouslyQueued || TryDequeue(task))
			&& TryExecuteTask(task);
	}
}
