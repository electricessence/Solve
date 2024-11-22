/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using Open.RandomizationExtensions;
using Open.Threading.Tasks;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Solve;

public interface IGenomeFactory<TGenome> : IGenomeSource<TGenome>
	where TGenome : class, IGenome
{
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
		using (TimeoutHandler.New(9000,
			ms => Console.WriteLine("Warning: {0}.GenerateOneFrom() is taking longer than {1} milliseconds.\n", this, ms)))
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
		while ((one = GenerateOneFrom(source)) is not null)
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
		int count = 0;
		foreach (TGenome g in source)
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
		while (AttemptNewMutation(source, out TGenome? next))
		{
			yield return next;
		}
	}

	// Random matchmaking...  It's possible to include repeats in the source to improve their chances. Possile O(n!) operaion.
	public TGenome[] AttemptNewCrossover(in ReadOnlySpan<TGenome> source, byte maxAttemptsPerCombination = 3)
	{
		int len = source.Length;
		if (len == 2 && source[0] != source[1])
			return AttemptNewCrossover(source[0], source[1], maxAttemptsPerCombination);
		if (len <= 2)
			return []; //throw new InvalidOperationException("Must have at least two unique genomes to crossover with.");

		TGenome[] s0 = source.ToArray();
		bool isFirst = true;
		do
		{
			// Take one.
			TGenome a = s0.RandomSelectOne();
			// Get all others (in orignal order/duplicates).
			TGenome[] s1 = s0.Where(g => g != a).ToArray();

			// Any left?
			while (s1.Length != 0)
			{
				isFirst = false;
				TGenome b = s1.RandomSelectOne();
				TGenome[] offspring = AttemptNewCrossover(a, b, maxAttemptsPerCombination);
				if (offspring.Length != 0) return offspring;
				// Reduce the possibilites.
				s1 = s1.Where(g => g != b).ToArray();
			}

			if (isFirst) // There were no other available candicates to cross over with. :(
				return []; //throw new InvalidOperationException("Must have at least two unique genomes to crossover with.");

			// Okay so we've been through all of them with 'a' Now move on to another.
			s0 = s0.Where(g => g != a).ToArray();
		}
		while (source.Length > 1); // Less than 2 left? Then we have no other options.

		return [];
	}
	#endregion

}
