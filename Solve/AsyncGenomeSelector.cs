using Nito.AsyncEx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Solve
{
	public class AsyncGenomeSelector<TGenome> : IEnumerable<ValueTask<TGenome>>
		where TGenome : class, IGenome
	{
		AsyncLock Sync = new AsyncLock();
		ValueTask<TGenome> Contender;

		readonly Func<ValueTask<TGenome>> Source;
		readonly Func<TGenome, ValueTask> Process;
		readonly Func<TGenome, TGenome, TGenome> Selector;


		public AsyncGenomeSelector(
			Func<ValueTask<TGenome>> source,
			Func<TGenome, ValueTask> processor,
			Func<TGenome, TGenome, TGenome> selector)
		{
			Source = source;
			Process = processor;
			Selector = selector;
		}

		async ValueTask<TGenome> GetNextContender()
		{
			var next = await Source();
			await Process(next);
			return next;
		}

		public async ValueTask<TGenome> Next()
		{
			var next = GetNextContender();
			TGenome winner;

			// Make sure the contender is never null.
			if(Contender==null)
			{
				using (await Sync.LockAsync())
				{
					if (Contender == null)
					{
						Contender = next;
						next = GetNextContender();
					}
				}
			}

			var n = await next; // Might as well wait for this to complete first...
			await Contender; // Make sure a contender is ready.  Assuming any potentailly replaced ones are always complete.

			using (await Sync.LockAsync())
			{
				var c = await Contender; // Should already be complete.
				Debug.Assert(n != c);
				if (n == c) throw new Exception("Next contender was the same as the current remaining one.");
				winner = Selector(n, c);
				if(winner==c) Contender = next;
			}

			return winner;
		}

		public IEnumerator<ValueTask<TGenome>> GetEnumerator()
		{
			while (true) yield return Next();
		}

		IEnumerator IEnumerable.GetEnumerator()
			=> this.GetEnumerator();

		public AsyncGenomeSelector<TGenome> CreateConsumer()
		{
			return new AsyncGenomeSelector<TGenome>(Next, Process, Selector);
		}
	}
}
