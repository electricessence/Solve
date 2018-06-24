using System;
using System.Collections;
using System.Collections.Generic;

namespace Solve.Collections
{
	public class IndexedReferenceList<T> : IReadOnlyReferenceList<T>
	{
		protected readonly ReadOnlyMemory<T> _source;
		public IndexedReferenceList(ReadOnlyMemory<T> source)
		{
			_source = source;
		}

		public int Count => _source.Length;
		int IReadOnlyCollection<T>.Count => _source.Length;

		public virtual ref readonly T this[in int index]
			=> ref _source.Span[index];

		T IReadOnlyList<T>.this[int index]
			=> this[index];

		public IEnumerator<T> GetEnumerator()
		{
			var len = _source.Length;
			for (var i = 0; i < len; i++)
				yield return this[i];
		}

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();
	}
}
