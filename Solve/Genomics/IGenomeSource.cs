using System.Collections;
using System.Collections.Generic;

namespace Solve
{
	public interface IGenomeSource<TGenome> : IEnumerable<TGenome>
		where TGenome : class, IGenome
	{
		TGenome Next();

		public new IEnumerator<TGenome> GetEnumerator()
		{
			TGenome? next;
			while ((next = Next()) != null)
				yield return next;
		}

		IEnumerator<TGenome> IEnumerable<TGenome>.GetEnumerator()
			=> GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();
	}
}
