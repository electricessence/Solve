using System.Collections.Generic;

namespace Solve.Collections
{
	public interface IReadOnlyReferenceList<T> : IReadOnlyList<T>
	{
		ref readonly T this[in int index] { get; }
	}
}
