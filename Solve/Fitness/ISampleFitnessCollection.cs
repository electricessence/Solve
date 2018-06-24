using System;
using System.Collections.Generic;

namespace Solve
{
	public interface ISampleFitnessCollection<T> : IReadOnlyList<ReadOnlyMemory<T>>
	{
		ushort Depth { get; }
		T GetValue(in int index, in int deep);
	}
}
