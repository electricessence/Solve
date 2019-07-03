using Open.Disposable;
using System;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.Supporting.TaskScheduling
{
	public abstract class DisposableTaskScheduler : TaskScheduler, IDisposable
	{
		/// <summary>Cancellation token used for disposal.</summary>
		protected readonly CancellationTokenSource DisposeCancellation = new CancellationTokenSource();

		protected virtual void OnDispose(bool calledExplicity) { }

		#region IDisposable Members

		public void Dispose()
		{
			DisposeCancellation.Cancel();
			DisposeCancellation.Dispose();
		}

		#endregion

		/// <inheritdoc />
		protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
		{
			if (task == null) throw new ArgumentNullException(nameof(task));
			Contract.EndContractBlock();

			if (DisposeCancellation.Token.IsCancellationRequested)
				return false;

			if (taskWasPreviouslyQueued && !TryDequeue(task))
				return false;

			return TryExecuteTask(task);
		}
	}
}
