/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */


using System;
using System.Collections.Generic;

namespace Solve
{

	public interface IGenome : IFreezable, ICloneable
	{
		/// <summary>
		/// A relative measuere of the complexity of a genome.
		/// </summary>
		int GeneCount { get; }

		/// <summary>
		/// A unique string by which the genome can be identified.
		/// The length of the hash and the length of the genome do not need to match.
		/// </summary>
		string Hash { get; }

		// Simply added for potential convienience.  Equals may create problems.
		bool Equivalent(IGenome other);

		IEnumerator<IGenome> RemainingVariations { get; }

#if DEBUG
		string StackTrace { get; }
		IReadOnlyList<IGenomeLogEntry> Log { get; }
		void AddLogEntry(string category, string message, string data = null);
#endif
	}

#if DEBUG
	public interface IGenomeLogEntry
	{
		DateTime TimeStamp { get; }
		string Category { get; }
		string Message { get; }
		string Data { get; }
	}
#endif

}
