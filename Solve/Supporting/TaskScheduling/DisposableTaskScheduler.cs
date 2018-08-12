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
		protected readonly DisposeHelper DisposingHelper = new DisposeHelper();

		protected virtual void OnDispose(bool calledExplicity) { }

		#region IDisposable Members

		public void Dispose()
		{
			DisposeCancellation.Cancel();
			DisposingHelper.Dispose(this, OnDispose, true);
		}

		#endregion

		~DisposableTaskScheduler()
		{
			DisposingHelper.Dispose(this, OnDispose, false);
		}

		/// <inheritdoc />
		protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
		{
			if (task == null) throw new ArgumentNullException(nameof(task));
			Contract.EndContractBlock();

			if (!DisposingHelper.IsAlive)
				return false;

			if (taskWasPreviouslyQueued && !TryDequeue(task))
				return false;

			return TryExecuteTask(task);
		}
	}
}
