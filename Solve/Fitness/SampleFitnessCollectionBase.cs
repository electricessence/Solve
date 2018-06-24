using Open.Collections;
using Open.Numeric;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Solve
{
	public abstract class SampleFitnessCollectionBase<TIn, TOut> : ISampleFitnessCollection<TOut>
	{
		protected SampleFitnessCollectionBase(IReadOnlyList<ReadOnlyMemory<TIn>> source, ushort depth)
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

	public abstract class SampleFitnessCollectionBase : SampleFitnessCollectionBase<int, double>
	{
		protected SampleFitnessCollectionBase(IReadOnlyList<ReadOnlyMemory<int>> source, ushort depth)
			: base(source, depth)
		{
			Progression = new LazyList<ReadOnlyMemory<ProcedureResult>>(GetProgression());
		}

		IEnumerable<ReadOnlyMemory<ProcedureResult>> GetProgression()
		{
			var current = Enumerable.Repeat(new ProcedureResult(0, 0), Depth).ToArray();
			var len = Source.Count;
			for (var i = 0; i < len; i++)
			{
				current = current.Select((v, d) => v.Add(GetValue(i, d))).ToArray();
				yield return current;
			}
		}

		public LazyList<ReadOnlyMemory<ProcedureResult>> Progression { get; }
	}
}
