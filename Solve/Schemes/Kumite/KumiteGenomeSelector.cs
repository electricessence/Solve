using Nito.AsyncEx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Solve.Schemes
{
	public class KumiteGenomeSelector<TGenome> : IEnumerable<Task<GenomeFitness<TGenome>>>
		where TGenome : class, IGenome
	{
		readonly Func<Task<GenomeFitness<TGenome>>> Source;
		readonly Func<GenomeFitness<TGenome>, Task> Process;

		Task<GenomeFitness<TGenome>> Contender;

		public KumiteGenomeSelector(
			Func<Task<GenomeFitness<TGenome>>> source,
			Func<GenomeFitness<TGenome>, Task> processor)
		{
			Source = source;
			Process = processor;
			Contender = GetNextContender();
		}

		async Task<GenomeFitness<TGenome>> GetNextContender()
		{
			var next = await Source();
			await Process(next);
			return next;
		}

		readonly AsyncLock Sync = new AsyncLock();

		public async Task<GenomeFitness<TGenome>> Next()
		{
			var next = GetNextContender();

			var n = await next; // Might as well wait for this to complete first...
			await Contender; // Make sure a contender is ready.  Assuming any potentailly replaced ones are always complete.

			GenomeFitness<TGenome> winner;
			using (await Sync.LockAsync())
			{
				var c = await Contender; // Should already be complete.
				var areSame = n.Genome == c.Genome;
				Debug.Assert(!areSame);
				if (areSame) throw new Exception("Next contender was the same as the current remaining one.");

				if (c.CompareTo(n) > 0)
				{
					Contender = next;
					winner = c;
				}
				else
				{
					winner = n;
				}
			}
			return winner;
		}

		public IEnumerator<Task<GenomeFitness<TGenome>>> GetEnumerator()
		{
			var current = Next();
			while (true)
			{
				yield return current;
				current = current
					.ContinueWith(t => Next())
					.Unwrap();
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
			=> this.GetEnumerator();

		public KumiteGenomeSelector<TGenome> CreateConsumer()
		{
			return new KumiteGenomeSelector<TGenome>(Next, Process);
		}

		public IEnumerator<KumiteGenomeSelector<TGenome>> AscendLevels()
		{
			var current = this;
			while (true)
			{
				current = this.CreateConsumer();
				yield return current;
			}
		}
	}
}
