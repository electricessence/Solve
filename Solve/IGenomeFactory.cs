/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */


using System.Collections.Generic;
using System.Threading.Tasks;

namespace Solve
{
	public interface IGenomeFactory<TGenome>
	 where TGenome : class, IGenome
	{
		/*
		 * Note that the input collections are all arrays for some important reasons:
		 * 1) The underlying computation does not want an enumerable that can change in size while selecing for a genome.
		 * 2) In order to select at random, the length of the collection must be known.
		 * 3) Forces the person using this class to smartly think about how to provide the array.
		 */

		TGenome GenerateOne(params TGenome[] source);

		Task<TGenome> GenerateOneAsync(params TGenome[] source);

		IEnumerable<TGenome> Generate(params TGenome[] source);

		IEnumerable<TGenome> AttemptNewMutation(TGenome source, byte triesPerMutationLevel = 5, byte maxMutations = 3);

		IEnumerable<TGenome> AttemptNewMutation(TGenome[] source, byte triesPerMutationLevel = 5, byte maxMutations = 3);

		IEnumerable<TGenome> AttemptNewCrossover(TGenome a, TGenome b, byte maxAttempts = 3);

		IEnumerable<TGenome> AttemptNewCrossover(TGenome primary, TGenome[] others, byte maxAttemptsPerCombination = 3);

		IEnumerable<TGenome> AttemptNewCrossover(TGenome[] source, byte maxAttemptsPerCombination = 3);

		IEnumerable<TGenome> Expand(TGenome genome, IEnumerable<TGenome> others = null);

	}
}
