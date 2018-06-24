using System;

namespace Solve.Collections
{
	public class TranslatedReferenceList<TSource, TOut> : IndexedReferenceList<TSource>
	{
		public TranslatedReferenceList(ReadOnlyMemory<TSource> source, Func<int, TOut> indexes)
			: base(source)
		{
			_indexes = indexes;
		}

		public override ref readonly T this[in int index]
			=> ref _source.Span[_indexes.Span[index]];
	}
}
