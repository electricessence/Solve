using System;

namespace Solve.Collections
{
	public class ReindexedReferenceList<T> : IndexedReferenceList<T>
	{
		readonly ReadOnlyMemory<int> _indexes;
		public ReindexedReferenceList(ReadOnlyMemory<T> source, ReadOnlyMemory<int> indexes)
			: base(source)
		{
			_indexes = indexes;
		}

		public override ref readonly T this[in int index]
			=> ref _source.Span[_indexes.Span[index]];
	}
}
