/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */


using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Solve
{

	public abstract class GenomeBase : FreezableBase, IGenome
	{
		protected GenomeBase() : base() { }

		public abstract string Hash { get; }

		public bool Equivalent(in IGenome other)
			=> this == other;

		protected abstract object CloneInternal();

		public object Clone()
			=> CloneInternal();

		protected static IEnumerator<T> EmptyEnumerator<T>()
			=> Enumerable.Empty<T>().GetEnumerator();

		static readonly IEnumerator<IGenome> EmptyVariations
			= EmptyEnumerator<IGenome>();

		public virtual IEnumerator<IGenome> RemainingVariations
			=> EmptyVariations;

		public abstract int Length { get; }

	}

}
