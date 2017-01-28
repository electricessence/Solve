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

        protected abstract void OnBeforeFreeze();

    }

}
