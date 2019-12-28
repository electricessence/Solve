/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */


using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Solve
{
	[SuppressMessage("ReSharper", "UnusedMemberInSuper.Global")]
	public interface IGenomeFactory<TGenome> : IEnumerable<TGenome>
	 where TGenome : class, IGenome
	{
		/*
		 * Note that the input collections are all arrays for some important reasons:
		 * 1) The underlying computation does not want an enumerable that can change in size while selecing for a genome.
		 * 2) In order to select at random, the length of the collection must be known.
		 * 3) Forces the person using this class to smartly think about how to provide the array.
		 */

		TGenome? GenerateOne(
			params TGenome[] source);

		IEnumerable<TGenome> Generate(
			params TGenome[] source);


		bool TryGenerateNew(
			[NotNullWhen(true)] out TGenome? potentiallyNew,
			params TGenome[] source);

		IEnumerable<TGenome> GenerateNew(
			params TGenome[] source);

		bool AttemptNewMutation(
			TGenome source,
			[NotNullWhen(true)] out TGenome? mutation,
			byte triesPerMutationLevel = 5,
			byte maxMutations = 3);

		bool AttemptNewMutation(
			in ReadOnlySpan<TGenome> source,
			[NotNullWhen(true)] out TGenome? mutation,
			byte triesPerMutationLevel = 5,
			byte maxMutations = 3);

		IEnumerable<TGenome> Mutate(TGenome source);

		// These will return null if the attempt fails.

		TGenome[] AttemptNewCrossover(TGenome a, TGenome b, byte maxAttempts = 3);

		TGenome[] AttemptNewCrossover(
			TGenome primary,
			in ReadOnlySpan<TGenome> others,
			byte maxAttemptsPerCombination = 3);

		TGenome[] AttemptNewCrossover(
			in ReadOnlySpan<TGenome> source,
			byte maxAttemptsPerCombination = 3);

		IGenomeFactoryPriorityQueue<TGenome> this[int index] { get; }

		TGenome? Next();
	}
}
