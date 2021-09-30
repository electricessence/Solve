﻿using Open.Collections.Synchronized;
using System;
using System.Collections.Concurrent;
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

			// Make sure whatever value we pick is not greater than the degree of parallelism allowed by the underlying scheduler.
			if (maxConcurrencyLevel == 0)
				maxConcurrencyLevel = parent.MaximumConcurrencyLevel;

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
		public string? Name { get; set; } // Useful for debugging.
		private readonly TaskScheduler _parent;

		private int _delegatesQueuedOrRunning;
		private readonly ConcurrentQueue<Task?> InternalQueue
			= new();

#if DEBUG
		protected override bool TryDequeue(Task task)
		{
			Debug.Fail("TryDequeue should never be called");
			return base.TryDequeue(task);
			//if (task is null) throw new ArgumentNullException(nameof(task));
			//Contract.EndContractBlock();

			//return InternalQueue.Remove(task);
		}
#endif

		private bool TryGetNext(out (PriorityQueueTaskScheduler scheduler, Task task) entry)
		{
			//Debug.WriteLineIf(Name is not null && Name.StartsWith("Level"), $"{Name} ({Id}): TryGetNext(out entry)");

			entry = default;
			if (DisposeCancellation.IsCancellationRequested)
				return false;

			if (!InternalQueue.TryDequeue(out var task))
				return false;

			if (task is not null)
			{
				entry = (this, task);
				return true;
			}

			var rp = ReversePriority;
			var node = rp ? Children.Last : Children.First;
			while (node is not null)
			{
				if (node.Value.TryGetNext(out entry))
					return true;

				node = rp ? node.Previous : node.Next;
			}

			return false;
		}

		private readonly ReadWriteSynchronizedLinkedList<PriorityQueueTaskScheduler> Children
			= new();

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
				if (node is null)
				{
					Children.Modify(
						() => Children.First is null,
						list => list.AddLast(new PriorityQueueTaskScheduler(this)));
					node = Children.First;
				}

				Debug.Assert(node is not null);

				for (var i = 0; i < index; i++)
				{
					if (node.Next is null)
					{
						var n = node;
						Children.Modify(
							() => n.Next is null,
							list => list.AddLast(new PriorityQueueTaskScheduler(this)));
					}

					node = node.Next;

					Debug.Assert(node is not null);
				}

				Debug.Assert(node is not null);

				return node.Value;
			}
		}

		/// <inheritdoc />
		public override int MaximumConcurrencyLevel { get; }

		/// <inheritdoc />
		protected override IEnumerable<Task> GetScheduledTasks()
			=> InternalQueue
				.Where(e => e is not null)
				.ToList()!;

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
						Debug.Assert(scheduler is not null);
						Debug.Assert(task is not null);

						_ = scheduler.TryExecuteTask(task);
					}
				}
				finally
				{
					// ReSharper disable once InvertIf
					if (InternalQueue.IsEmpty)
					{
						// Now that we think we're done, verify that there really is
						// no more work to do.  If there's not, highlight
						// that we're now less parallel than we were a moment ago.
						lock (InternalQueue)
						{
							// ReSharper disable once InvertIf
							if (InternalQueue.IsEmpty)
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
		protected override void QueueTask(Task? task)
		{
			DisposeCancellation.Token.ThrowIfCancellationRequested();

			InternalQueue.Enqueue(task);
			//Debug.WriteLineIf(Name is not null && task is not null, $"{Name} ({Id}): QueueTask({task?.Id})");

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

		protected override void OnDispose() => Children.Dispose();


	}
}
