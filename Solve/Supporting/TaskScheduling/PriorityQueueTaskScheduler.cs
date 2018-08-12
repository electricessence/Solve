using Open.Collections.Synchronized;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.Supporting.TaskScheduling
{
	public sealed class PriorityQueueTaskScheduler : DisposableTaskScheduler
	{
		/// <inheritdoc />
		/// <summary>Initializes the scheduler.</summary>
		/// <param name="parent">The target underlying scheduler onto which this sceduler's work is queued.</param>
		/// <param name="maxConcurrencyLevel">The maximum degree of concurrency allowed for this scheduler's work.</param>
		public PriorityQueueTaskScheduler(
			TaskScheduler parent,
			int maxConcurrencyLevel = 0)
		{
			_parent = parent ?? throw new ArgumentNullException(nameof(parent));
			if (maxConcurrencyLevel < 0)
				throw new ArgumentOutOfRangeException(nameof(maxConcurrencyLevel));
			Contract.EndContractBlock();

			// If 0, use the number of logical processors.  But make sure whatever value we pick
			// is not greater than the degree of parallelism allowed by the underlying scheduler.
			if (maxConcurrencyLevel == 0)
				maxConcurrencyLevel = Environment.ProcessorCount;

			if (parent.MaximumConcurrencyLevel > 0
				&& parent.MaximumConcurrencyLevel < maxConcurrencyLevel)
			{
				maxConcurrencyLevel = parent.MaximumConcurrencyLevel;
			}

			MaximumConcurrencyLevel = maxConcurrencyLevel;
		}

		/// <summary>
		/// Reverse priority causes the child queues to be queried in reverse order.
		/// So if set to true, the last child scheduler will be queried first.
		/// </summary>
		// ReSharper disable once UnusedAutoPropertyAccessor.Global
		public bool ReversePriority { get; set; }
		private readonly TaskScheduler _parent;

		private int _delegatesQueuedOrRunning;
		private readonly LockSynchronizedLinkedList<Task> InternalQueue
			= new LockSynchronizedLinkedList<Task>();

		protected override bool TryDequeue(Task task)
		{
			if (task == null) throw new ArgumentNullException(nameof(task));
			Contract.EndContractBlock();

			return InternalQueue.Remove(task);
		}

		private bool TryGetNext(out (PriorityQueueTaskScheduler scheduler, Task task) entry)
		{
			entry = default;
			if (DisposeCancellation.IsCancellationRequested)
				return false;

			if (!InternalQueue.TryTakeFirst(out var task))
				return false;

			if (task != null)
			{
				entry = (this, task);
				return true;
			}

			var rp = ReversePriority;
			var node = rp ? Children.Last : Children.First;
			while (node != null)
			{
				if (node.Value.TryGetNext(out entry))
					return true;

				node = rp ? node.Previous : node.Next;
			}

			return false;
		}

		private readonly ReadWriteSynchronizedLinkedList<PriorityQueueTaskScheduler> Children
			= new ReadWriteSynchronizedLinkedList<PriorityQueueTaskScheduler>();

		void NotifyNewWorkItem()
			// ReSharper disable once AssignNullToNotNullAttribute
			=> QueueTask(null);

		public PriorityQueueTaskScheduler this[int index]
		{
			get
			{
				if (index < 0)
					throw new ArgumentOutOfRangeException(nameof(index), index, "Must be at least zero");
				Contract.EndContractBlock();

				var node = Children.First;
				if (node == null)
				{
					Children.Modify(
						() => Children.First == null,
						list => list.AddLast(new PriorityQueueTaskScheduler(this)));
					node = Children.First;
				}

				Debug.Assert(node != null);

				for (var i = 0; i < index; i++)
				{
					if (node.Next == null)
					{
						var n = node;
						Children.Modify(
							() => n.Next == null,
							list => list.AddLast(new PriorityQueueTaskScheduler(this)));
					}

					node = node.Next;

					Debug.Assert(node != null);
				}

				Debug.Assert(node != null);

				return node.Value;
			}
		}

		/// <inheritdoc />
		public override int MaximumConcurrencyLevel { get; }

		/// <inheritdoc />
		protected override IEnumerable<Task> GetScheduledTasks()
			=> InternalQueue
				.Snapshot()
				.Where(e => e != null)
				.ToList();

		private void ProcessQueues()
		{
			var continueProcessing = true;
			while (continueProcessing && !DisposeCancellation.IsCancellationRequested)
			{
				try
				{
					while (TryGetNext(out var entry))
					{
						var (scheduler, task) = entry;
						Debug.Assert(scheduler != null);
						Debug.Assert(task != null);

						scheduler.TryExecuteTask(task);
					}
				}
				finally
				{
					// ReSharper disable once InvertIf
					if (InternalQueue.Count == 0)
					{
						// Now that we think we're done, verify that there really is
						// no more work to do.  If there's not, highlight
						// that we're now less parallel than we were a moment ago.
						lock (InternalQueue)
						{
							// ReSharper disable once InvertIf
							if (InternalQueue.Count == 0)
							{
								--_delegatesQueuedOrRunning;
								continueProcessing = false;
							}
						}
					}

				}
			}
		}

		/// <inheritdoc />
		protected override void QueueTask(Task task)
		{
			DisposingHelper.AssertIsAlive();

			InternalQueue.AddLast(task);

			if (_parent is PriorityQueueTaskScheduler p)
				p.NotifyNewWorkItem();
			else
			{
				if (_delegatesQueuedOrRunning >= MaximumConcurrencyLevel)
					return;

				lock (InternalQueue)
				{
					if (_delegatesQueuedOrRunning >= MaximumConcurrencyLevel)
						return;

					++_delegatesQueuedOrRunning;
				}

				Task.Factory.StartNew(ProcessQueues,
					CancellationToken.None, TaskCreationOptions.None, _parent);
			}
		}

		protected override void OnDispose(bool calledExplicity)
		{
			if (calledExplicity)
			{
				Children.Dispose();
			}
		}


	}
}
