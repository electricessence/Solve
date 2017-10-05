/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using System;
using System.Collections.Generic;
using System.Collections;

namespace Solve
{

	public abstract class GenomeBase : FreezableBase, IGenome
	{
		public GenomeBase() : base()
		{

        }

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
