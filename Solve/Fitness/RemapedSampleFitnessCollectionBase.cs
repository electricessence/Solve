using System;
using System.Collections.Generic;

namespace Solve.Fitness
{
	public abstract class RemapedSampleFitnessCollectionBase<TIn, TOut> : SampleFitnessCollectionBase<TIn, TOut>
	{
		public RemapedSampleFitnessCollectionBase(IReadOnlyList<ReadOnlyMemory<TIn>> source, ushort depth)
			: base(source, depth)
		{
		}

		protected virtual ref readonly int DepthIndex(in int index) => ref index;

		public override TOut GetValue(in int index, in int deep)
			=> Source[index].Span[DepthIndex(deep)];
	}
}
