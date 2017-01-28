/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/Genetic-Algorithm-Platform/blob/master/LICENSE.md
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Open;
using Open.Threading;

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
    }

}
