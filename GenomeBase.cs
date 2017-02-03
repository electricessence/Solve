/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using System;
using System.Collections.Generic;
using System.Collections;

namespace Solve
{

	public abstract class GenomeBase<T> : FreezableBase, IGenome<T>
	{
		public GenomeBase() : base()
		{

        }

		Lazy<T[]> _genes;
		public T[] Genes
		{
			get
			{
				var g = _genes;
				return !IsReadOnly || g == null ? GetGenes() : g.Value;
			}
		}

        abstract protected T[] GetGenes();

		public abstract string Hash { get; }


        public bool Equivalent(IGenome other)
		{
			return this == other;
		}

        protected abstract object CloneInternal();

        public object Clone()
        {
            return CloneInternal();
        }

		public IEnumerator<T> GetEnumerator()
		{
			return ((IEnumerable<T>)Genes).GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}
	}

}
