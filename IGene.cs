/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/Genetic-Algorithm-Platform/blob/master/LICENSE.md
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Solve
{

	public interface IGene : ICloneable<IGene>
	{
		/*
         * Should prevent further modifications to the genome.
         */
		void Freeze();
		bool Equivalent(IGene other);
	}

	public interface IGene<TMod, TResult> : IGene, ICloneable<IGene<TMod, TResult>>
	{
		TMod Modifier { get; set; }
		bool SetModifier(TMod value);

		TResult Evaluate(IReadOnlyList<dynamic> values);
		Task<TResult> EvaluateAsync(IReadOnlyList<dynamic> values);
	}


	public interface IGeneNode<TChild> : IGene, ICollection<TChild>, ICloneable<IGeneNode<TChild>> /*,ISerializable*/
		where TChild : IGene
	{
		IEnumerable<TChild> Children { get; }
		IEnumerable<TChild> Descendants { get; }
		IGeneNode<TChild> FindParent(TChild child);

		bool ReplaceChild(TChild target, TChild replacement, bool throwIfNotFound = false);
	}

	public interface IGeneNode : IGeneNode<IGene>
	{

	}

	public interface IGeneNode<TChild, TMod, TResult> : IGeneNode<TChild>, IGene<TMod, TResult>, ICloneable<IGeneNode<TChild, TMod, TResult>> /*,ISerializable*/
	where TChild : IGene
	{

	}

}
