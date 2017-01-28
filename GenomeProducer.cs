/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/Genetic-Algorithm-Platform/blob/master/LICENSE.md
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Open.Collections;
using Open.Dataflow;
using Open.Threading;

namespace Solve
{

	public class GenomeProducer<TGenome> : ISourceBlock<TGenome>
		where TGenome : IGenome
	{

		BufferBlock<TGenome> EnqueuedBuffer = new BufferBlock<TGenome>();
		BufferBlock<TGenome> ProduceBuffer = new BufferBlock<TGenome>(new DataflowBlockOptions { BoundedCapacity = 100 });
		BufferBlock<TGenome> OutputBuffer;

		ActionBlock<bool> Producer;
		HashSet<string> Registry = new HashSet<string>();

		const int TIMEOUT_MS = 60000;
		void TimedOut(int ms)
		{
			var message = "Source Enumeration took longer than " + ms + " seconds to produce.";
			Console.WriteLine(message);
			if (!Debugger.IsAttached)
			{
				((ISourceBlock<TGenome>)OutputBuffer).Fault(new TimeoutException(message));
				Complete();
			}
		}

		private GenomeProducer(IEnumerator<TGenome> source, int bufferSize = 100)
		{
			OutputBuffer = new BufferBlock<TGenome>(new DataflowBlockOptions
			{
				BoundedCapacity = bufferSize
			});
			// Should make the enqueue buffer the priority.
			EnqueuedBuffer.LinkToWithExceptions(OutputBuffer);
			ProduceBuffer.LinkToWithExceptions(OutputBuffer);

			Producer = new ActionBlock<bool>(async retry =>
			{
				try
				{
					int attempts = 0;
					bool more = false;
					IDisposable timeout;
					while (attempts++ < 20 && TimeoutHandler.New(TIMEOUT_MS, out timeout, TimedOut) && (more = source.MoveNext()))
					{
						timeout.Dispose();
						var next = source.Current;
						if (next != null)
						{
							if (!next.IsReadOnly)
								throw new InvalidOperationException("Cannot process an unfrozen genome.");

							var hash = next.Hash;
							if (Registry.Contains(hash)) continue;
							// Try and reserve this hash.
							lock (Registry)
							{
								// Don't own it? :(
								if (!Registry.Add(hash)) continue;
							}
							// Console.WriteLine("Produce Buffer: " + ProduceBuffer.Count);
							// Producer magic happens here...
							if (await ProduceBuffer.SendAsync(next))
							{
								attempts = 0;
							}
							else
							{
								lock (Registry) Registry.Remove(hash);
								// This does not mean postponed.  Should have not rejected unless complete.
								more = false;
								break;
							}
						}
					}

					if (!more)
					{
						Producer.Complete();
						ProduceBuffer.Complete();
					}
					else
					{
						Producer.Post(true);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
					throw ex;
				}


			});

			// Producer.OnComplete(() => Console.WriteLine("Producer Complete."));
			// ProduceBuffer.OnComplete(() => Console.WriteLine("ProduceBuffer Complete."));
			// EnqueuedBuffer.OnComplete(() => Console.WriteLine("EnqueuedBuffer Complete."));


		}

		public GenomeProducer(
			IEnumerable<TGenome> source,
			int bufferSize = 100) : this(source.PreCache(10).GetEnumerator(), bufferSize)
		{

		}

		public void Poke()
		{
			Producer.Post(true);
		}

		public Task ProductionCompetion
		{
			get
			{
				return ProduceBuffer.Completion;
			}
		}

		bool TryEnqueueInternal(BufferBlock<TGenome> target, TGenome genome, bool force = false)
		{
			bool queued = false;
			if (genome != null)
			{
				if (!genome.IsReadOnly)
					throw new InvalidOperationException("Cannot process an unfrozen genome.");

				var hash = genome.Hash;
				if (force || !Registry.Contains(hash))
				{
					lock (Registry)
					{
						if ((force || !Registry.Contains(hash)) && target.Post(genome))
						{
							queued = Registry.Add(genome.Hash);
							Debug.Assert(force || queued);
						}
					}
				}
			}
			return queued;
		}

		public bool TryEnqueue(TGenome genome, bool force = false)
		{
			return TryEnqueueInternal(EnqueuedBuffer, genome);
		}

		public void TryEnqueue(IEnumerable<TGenome> genomes, bool force = false)
		{
			if (genomes != null)
			{
				foreach (var genome in genomes)
					TryEnqueueInternal(EnqueuedBuffer, genome, force);
			}
		}

		// bool TryEnqueueProduction(TGenome genome)
		// {
		// 	return TryEnqueueInternal(ProduceBuffer, genome);
		// }

		public Task Completion
		{
			get
			{
				return OutputBuffer.Completion;
			}
		}

		public void Complete()
		{
			Console.WriteLine("Producer Complete Called.");
			Producer.Complete();
			ProduceBuffer.Complete();
			EnqueuedBuffer.Complete();
			OutputBuffer.Complete();
		}

		public TGenome ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<TGenome> target, out bool messageConsumed)
		{
			return ((ISourceBlock<TGenome>)OutputBuffer).ConsumeMessage(messageHeader, target, out messageConsumed);
		}

		public void Fault(Exception exception)
		{
			((ISourceBlock<TGenome>)OutputBuffer).Fault(exception);
		}

		public IDisposable LinkTo(ITargetBlock<TGenome> target, DataflowLinkOptions linkOptions)
		{
			return OutputBuffer.LinkTo(target, linkOptions);
		}

		public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<TGenome> target)
		{
			((ISourceBlock<TGenome>)OutputBuffer).ReleaseReservation(messageHeader, target);
		}

		public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<TGenome> target)
		{
			return ((ISourceBlock<TGenome>)OutputBuffer).ReserveMessage(messageHeader, target);
		}

	}

}

