﻿/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */


using Open.Cloneable;
using System.Collections.Generic;

namespace Solve
{

	public interface IGenome : IFreezable, ICloneable
	{
		/// <summary>
		/// A relative measuere of the complexity of a genome.
		/// </summary>
		int Length { get; }

		/// <summary>
		/// A unique string by which the genome can be identified.
		/// The length of the hash and the length of the genome do not need to match.
		/// </summary>
		string Hash { get; }

		// Simply added for potential convienience.  Equals may create problems.
		bool Equivalent(in IGenome other);

		IEnumerator<IGenome> RemainingVariations { get; }
	}

}