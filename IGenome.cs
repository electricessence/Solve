/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/Genetic-Algorithm-Platform/blob/master/LICENSE.md
 */



using EvaluationFramework;
using System;

namespace Solve
{

    public interface IGenome : IFreeze, ICloneable
    {
        string Hash { get; }

        bool Equivalent(IGenome other);
    }

    public interface IGenome<out TGene> : IGenome /* : ISerializable */
    {
        TGene[] Genes { get; }
    }

}