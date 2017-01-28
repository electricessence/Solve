using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Solve
{
    /*
     * It's important to have an interface that ensures an object is in flux or won't change.
     * This way we can avoid synchronization woes and use virtually immutable objects instead.
     */

    public interface IFreeze
    {
        // True if frozen.
        bool IsReadOnly { get; }

        /*
         * Should prevent further modifications to the genome.
         */
        void Freeze();
    }
}
