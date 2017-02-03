﻿/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
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

    public abstract class FreezableBase : IFreeze
    {

        public bool IsReadOnly
        {
            get;
            private set;
        }

        public void Freeze()
        {
            OnBeforeFreeze();
            IsReadOnly = true;
        }

		protected void AssertIsNotFrozen()
		{
			if (IsReadOnly)
				throw new InvalidOperationException("Genome is frozen.");
		}

        protected abstract void OnBeforeFreeze();

    }

}
