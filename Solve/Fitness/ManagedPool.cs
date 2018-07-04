using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Solve
{
	public class ManagedPool<TGenome> : ICollection<IGenomeFitness<TGenome, Fitness>>
		where TGenome : class, IGenome
	{
		readonly ConcurrentQueue<IGenomeFitness<TGenome, Fitness>> Incomming
			= new ConcurrentQueue<IGenomeFitness<TGenome, Fitness>>();

		readonly List<IGenomeFitness<TGenome, Fitness>> Entries
			= new List<IGenomeFitness<TGenome, Fitness>>();

		public int Count => ProcessAdded();
		public bool IsReadOnly => false;

		public void Add(IGenomeFitness<TGenome, Fitness> genomeFitness)
			=> Incomming.Enqueue(genomeFitness);

		Lazy<IGenomeFitness<TGenome, Fitness>[]> GetRanking()
			=> new Lazy<IGenomeFitness<TGenome, Fitness>[]>(() =>
			{
				IGenomeFitness<TGenome, Fitness>[] snapshot;
				lock (Entries) snapshot = Entries.ToArray();
				snapshot.Sort(true);
				return snapshot;
			});

		public int ProcessAdded()
		{
			if (Incomming.TryDequeue(out IGenomeFitness<TGenome, Fitness> entry))
			{
				lock (Entries)
				{
					do
					{
						Entries.Add(entry);
					}
					while (Incomming.TryDequeue(out entry));
				}

				if (_lastRanking != null)
					Interlocked.Exchange(ref _lastRanking, null);
			}

			return Entries.Count;
		}

		Lazy<IGenomeFitness<TGenome, Fitness>[]> _lastRanking;
		public ReadOnlySpan<IGenomeFitness<TGenome, Fitness>> Rank()
		{
			ProcessAdded();

			return LazyInitializer.EnsureInitialized(ref _lastRanking, GetRanking).Value;
		}

		public void Clear()
		{
			lock (Entries)
			{
				ProcessAdded();
				Entries.Clear();
			}
		}

		public bool Contains(IGenomeFitness<TGenome, Fitness> item)
		{
			ProcessAdded();
			lock (Entries) return Entries.Contains(item);
		}

		public void CopyTo(IGenomeFitness<TGenome, Fitness>[] array, int arrayIndex)
		{
			ProcessAdded();
			lock (Entries) Entries.CopyTo(array, arrayIndex);
		}

		public bool Remove(IGenomeFitness<TGenome, Fitness> item)
		{
			ProcessAdded();
			lock (Entries)
			{
				return Entries.Remove(item);
			}
		}

		public IEnumerator<IGenomeFitness<TGenome, Fitness>> GetEnumerator()
		{
			ProcessAdded();
			lock (Entries) return Entries.ToList().GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();
	}
}
