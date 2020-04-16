/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */


using Open.RandomizationExtensions;
using Open.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Solve
{
	[SuppressMessage("ReSharper", "UnusedMemberInSuper.Global")]
	public interface IGenomeFactory<TGenome> : IGenomeSource<TGenome>
		where TGenome : class, IGenome
	{
		/*
		 * Note that the input collections are all arrays for some important reasons:
		 * 1) The underlying computation does not want an enumerable that can change in size while selecing for a genome.
		 * 2) In order to select at random, the length of the collection must be known.
		 * 3) Forces the person using this class to smartly think about how to provide the array.
		 */

		bool TryGenerateNew(
			[NotNullWhen(true)] out TGenome? potentiallyNew,
			IReadOnlyList<TGenome>? source = null);

		bool AttemptNewMutation(
			TGenome source,
			[NotNullWhen(true)] out TGenome? mutation,
			byte triesPerMutationLevel = 2,
			byte maxMutations = 3);

		TGenome[] AttemptNewCrossover(TGenome a, TGenome b, byte maxAttempts = 3);

		IGenomeFactoryPriorityQueue<TGenome> this[int index] { get; }

		#region Default Implmentations
		public TGenome GenerateOne()
			=> GenerateOneFrom(null!) ?? throw new Exception("Unable to generate new genome.");

		// These will return null if the attempt fails.
		public TGenome? GenerateOneFrom(IReadOnlyList<TGenome> source)
		{
			TGenome? one = null;
			using (TimeoutHandler.New(9000, ms =>
			{
				Console.WriteLine("Warning: {0}.GenerateOneFrom() is taking longer than {1} milliseconds.\n", this, ms);
			}))
			{
				byte attempts = 0;
				while (attempts < 2 && !TryGenerateNew(out one, source))
					attempts++;
			}

			if (one is null)
			{
				Console.WriteLine("GenomeFactory failed GenerateOneFrom()");
			}

			return one;
		}

		public TGenome? GenerateOneFrom(params TGenome[] source)
			=> GenerateOneFrom((IReadOnlyList<TGenome>)source);

		public IEnumerable<TGenome> GenerateFrom(IReadOnlyList<TGenome> source)
		{
			TGenome? one;
			while ((one = GenerateOneFrom(source)) != null)
			{
				yield return one;
			}
		}


		public bool AttemptNewMutation(
			IEnumerable<TGenome> source,
			[NotNullWhen(true)] out TGenome? genome,
			byte triesPerMutationLevel = 2,
			byte maxMutations = 3)
		{
			var count = 0;
			foreach (var g in source)
			{
				count++;
				if (AttemptNewMutation(g, out genome, triesPerMutationLevel, maxMutations))
					return true;
			}
			Debug.Assert(count != 0, "Should never pass an empty source for mutation.");
			genome = default!;
			return false;
		}


		public IEnumerable<TGenome> Mutate(TGenome source)
		{
			while (AttemptNewMutation(source, out var next))
			{
				yield return next;
			}
		}

		// Random matchmaking...  It's possible to include repeats in the source to improve their chances. Possile O(n!) operaion.
		public TGenome[] AttemptNewCrossover(in ReadOnlySpan<TGenome> source, byte maxAttemptsPerCombination = 3)
		{
			var len = source.Length;
			if (len == 2 && source[0] != source[1])
				return AttemptNewCrossover(source[0], source[1], maxAttemptsPerCombination);
			if (len <= 2)
				return Array.Empty<TGenome>(); //throw new InvalidOperationException("Must have at least two unique genomes to crossover with.");

			var s0 = source.ToArray();
			var isFirst = true;
			do
			{
				// Take one.
				var a = s0.RandomSelectOne();
				// Get all others (in orignal order/duplicates).
				var s1 = s0.Where(g => g != a).ToArray();

				// Any left?
				while (s1.Length != 0)
				{
					isFirst = false;
					var b = s1.RandomSelectOne();
					var offspring = AttemptNewCrossover(a, b, maxAttemptsPerCombination);
					if (offspring.Length != 0) return offspring;
					// Reduce the possibilites.
					s1 = s1.Where(g => g != b).ToArray();
				}

				if (isFirst) // There were no other available candicates to cross over with. :(
					return Array.Empty<TGenome>(); //throw new InvalidOperationException("Must have at least two unique genomes to crossover with.");

				// Okay so we've been through all of them with 'a' Now move on to another.
				s0 = s0.Where(g => g != a).ToArray();
			}
			while (source.Length > 1); // Less than 2 left? Then we have no other options.

			return Array.Empty<TGenome>();
		}
		#endregion

	}
}
