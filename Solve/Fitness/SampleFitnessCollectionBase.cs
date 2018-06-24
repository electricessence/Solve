using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Solve
{
	public abstract class SampleFitnessCollectionBase<TIn, TOut> : ISampleFitnessCollection<TOut>
	{
		public SampleFitnessCollectionBase(IReadOnlyList<ReadOnlyMemory<TIn>> source, ushort depth)
		{
			Source = source;
			Depth = depth;
		}

		protected readonly IReadOnlyList<ReadOnlyMemory<TIn>> Source;

		public ReadOnlyMemory<TOut> this[int index]
			=> Enumerable.Range(0, Depth)
				.Select(d => GetValue(index, d))
				.ToArray();

		public int Count => Source.Count;
		public ushort Depth { get; }

		public IEnumerator<ReadOnlyMemory<TOut>> GetEnumerator()
		{
			var len = Count;
			for (var i = 0; i < len; i++)
				yield return this[i];
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public abstract TOut GetValue(in int index, in int deep);

	}
}
