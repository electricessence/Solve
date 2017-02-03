/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */


using System;
using System.Collections.Generic;

namespace Solve
{

    public interface IGenome : IFreeze, ICloneable
    {
        string Hash { get; }

        bool Equivalent(IGenome other);
    }

    public interface IGenome<out TGene> : IGenome, IEnumerable<TGene> /* : ISerializable */
	{
        TGene[] Genes { get; }
    }

}