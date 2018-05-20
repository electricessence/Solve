/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using Open.Collections.Synchronized;
using Open.Numeric;
using Open.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;

namespace Solve
{

	public abstract class GenomeFactoryBase<TGenome> : IGenomeFactory<TGenome>
	where TGenome : class, IGenome
	{

		public GenomeFactoryBase()
		{
			Registry = new ConcurrentDictionary<string, Lazy<TGenome>>();
			RegistryOrder = new ReadWriteSynchronizedList<string>();
			PreviouslyProduced = new LockSynchronizedHashSet<string>();
		}

		// Help to reduce copies.
		// Use a Lazy to enforce one time only execution since ConcurrentDictionary is optimistic.
		protected readonly ConcurrentDictionary<string, Lazy<TGenome>> Registry;

		protected readonly LockSynchronizedHashSet<string> PreviouslyProduced;

		protected readonly ReadWriteSynchronizedList<string> RegistryOrder;

		protected static TGenome AssertFrozen(TGenome genome)
		{
			if (genome != null && !genome.IsReadOnly)
				throw new InvalidOperationException("Genome is not frozen: " + genome);
			return genome;
		}

		protected bool Register(string genomeHash, Func<TGenome> factory, out TGenome actual, Action<TGenome> onBeforeAdd = null)
		{
			var added = false;
			actual = Registry.GetOrAdd(genomeHash, hash => Lazy.Create(() =>
			{
				added = true;
				var genome = factory();
				Debug.Assert(genome.Hash == hash);
				onBeforeAdd(genome);
				AssertFrozen(genome); // Cannot allow registration of an unfrozen genome because it then can be used by another thread.
				RegistryOrder.Add(hash);
				return genome;
			})).Value;

			return added;
		}

		protected bool Register(TGenome genome, out TGenome actual, Action<TGenome> onBeforeAdd = null)
		{
			var added = false;
			actual = Registry.GetOrAdd(genome.Hash, hash => Lazy.Create(() =>
			{
				added = true;
				Debug.Assert(genome.Hash == hash);
				onBeforeAdd(genome);
				AssertFrozen(genome); // Cannot allow registration of an unfrozen genome because it then can be used by another thread.
				RegistryOrder.Add(hash);
				return genome;
			})).Value;

			return added;
		}

		protected virtual TGenome Registration(TGenome genome)
		{
			if (genome == null) return null;
			Debug.Assert(genome.Hash.Length != 0, "Genome cannot have empty hash.");
			Register(genome, out TGenome actual);
			return actual;
		}

		protected bool RegisterProduction(TGenome genome)
		{
			if (genome == null)
				throw new ArgumentNullException(nameof(genome));
			var hash = genome.Hash;
			if (!Registry.ContainsKey(hash))
				throw new InvalidOperationException("Registering for production before genome was in global registry.");
			return PreviouslyProduced.Add(genome.Hash);
		}

		protected bool AlreadyProduced(string hash)
		{
			if (hash == null)
				throw new ArgumentNullException(nameof(hash));
			return PreviouslyProduced.Contains(hash);
		}

		protected bool AlreadyProduced(TGenome genome)
		{
			if (genome == null)
				throw new ArgumentNullException(nameof(genome));
			return AlreadyProduced(genome.Hash);
		}

		public string[] GetAllPreviousGenomesInOrder()
		{

			return RegistryOrder.Snapshot();
		}

		// Be sure to call Registration within the GenerateOne call.
		protected abstract TGenome GenerateOneInternal();

		public IEnumerable<TGenome> Generate(params TGenome[] source)
		{
			if (source != null && source.Length != 0)
			{
				foreach (var one in AttemptNewMutation(source))
				{
					yield return AssertFrozen(one);
				}
			}

			while (true)
			{
				TGenome one;
				using (TimeoutHandler.New(9000, ms =>
				{
					Console.WriteLine("Warning: {0}.GenerateOne() is taking longer than {1} milliseconds.\n", this, ms);
				}))
				{
					one = GenerateOneInternal();
				}
				if (one == null)
				{
					Console.WriteLine("GenomeFactory failed GenerateOne()");
					break;
				}

				Registration(one);
				yield return AssertFrozen(one);
			}
		}


		public TGenome GenerateOne(params TGenome[] source)
			=> Generate(source).FirstOrDefault();

		public Task<TGenome> GenerateOneAsync(params TGenome[] source)
			=> Task.Run(() => GenerateOne(source));

		// Be sure to call Registration within the GenerateOne call.
		protected abstract TGenome MutateInternal(TGenome target);

		public IEnumerable<TGenome> AttemptNewMutation(TGenome source, byte triesPerMutationLevel = 5, byte maxMutations = 3)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			Contract.EndContractBlock();
			return AttemptNewMutation(new TGenome[] { source }, triesPerMutationLevel, maxMutations);
		}

		public IEnumerable<TGenome> AttemptNewMutation(TGenome[] source, byte triesPerMutationLevel = 5, byte maxMutations = 3)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			Debug.Assert(source.Length != 0, "Should never pass an empty source for mutation.");
			Contract.EndContractBlock();

			source = source.Where(g => g.Hash.Length != 0).ToArray();

			IEnumerable<TGenome> rest()
			{
				bool next(out TGenome genome)
				{
					using (TimeoutHandler.New(5000, ms =>
					{
						Console.WriteLine("Warning: {0}.AttemptNewMutation() is taking longer than {1} milliseconds.\n", this, ms);
					}))
					{
						// Find one that will mutate well and use it.
						for (byte m = 1; m <= maxMutations; m++)
						{
							for (byte t = 0; t < triesPerMutationLevel; t++)
							{
								genome = Mutate(source.RandomSelectOne(), m);
								if (genome != null && RegisterProduction(genome))
									return true;
							}
						}
					}
					genome = null;
					return false;
				}

				while (next(out TGenome g))
				{
					yield return g;
				}
			}

			return source.Length == 0
				? Enumerable.Empty<TGenome>()
				: rest();
		}

		protected TGenome Mutate(TGenome source, byte mutations = 1)
		{
			TGenome original = source;
			TGenome genome = null;
			while (mutations != 0)
			{
				byte tries = 3;
				while (tries != 0 && genome == null)
				{
					using (TimeoutHandler.New(3000, ms =>
					{
						Console.WriteLine("Warning: {0}.MutateInternal({1}) is taking longer than {2} milliseconds.\n", this, source, ms);
					}))
					{
						genome = MutateInternal(source);
						var hash = genome?.Hash;
						if (hash != null && (hash == source.Hash || hash == original.Hash))
							genome = null; // Not a mutation. Could happen on repeat mutations.
					}
					--tries;
				}
				// Reuse the clone as the source 
				if (genome == null) break; // No single mutation possible? :/
				source = genome;
				--mutations;
			}
			return Registration(genome);
		}


		protected abstract IEnumerable<TGenome> CrossoverInternal(TGenome a, TGenome b, byte maxAttempts = 3);

		protected IEnumerable<TGenome> Crossover(TGenome a, TGenome b, byte maxAttempts = 3)
		{
			foreach (var r in CrossoverInternal(a, b, maxAttempts))
			{
				if (r.Hash.Length == 0)
					throw new InvalidOperationException("Cannot process a genome with an empty hash.");
				Registration(r);
				yield return r;
			}
		}

		public virtual IEnumerable<TGenome> AttemptNewCrossover(TGenome a, TGenome b, byte maxAttempts = 3)
		{
			if (a == null || b == null || a == b) return Enumerable.Empty<TGenome>();

			return Crossover(a, b, maxAttempts).Where(g => RegisterProduction(g));
		}

		// Random matchmaking...  It's possible to include repeats in the source to improve their chances. Possile O(n!) operaion.
		public IEnumerable<TGenome> AttemptNewCrossover(TGenome[] source, byte maxAttemptsPerCombination = 3)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if (source.Length == 2 && source[0] != source[1]) return AttemptNewCrossover(source[0], source[1], maxAttemptsPerCombination);
			if (source.Length <= 2)
				return Enumerable.Empty<TGenome>(); //throw new InvalidOperationException("Must have at least two unique genomes to crossover with.");

			IEnumerable<TGenome> rest()
			{
				do
				{
					// Take one.
					var a = RandomUtilities.RandomSelectOne(source);
					// Get all others (in orignal order/duplicates).
					var s1 = source.Where(g => g != a).ToArray();
					if (s1.Length == 0) break; // There were no more available candicates to cross over with. :(

					// Any left?
					while (s1.Length != 0)
					{
						var b = s1.RandomSelectOne();
						foreach (var c in AttemptNewCrossover(a, b, maxAttemptsPerCombination))
						{
							yield return c;
						}

						// Reduce the possibilites.
						s1 = s1.Where(g => g != b).ToArray();
					}

					// Okay so we've been through all of them with 'a' Now move on to another.
					source = source.Where(g => g != a).ToArray();
				}
				while (source.Length > 1); // Less than 2 left? Then we have no other options.
			}

			return rest();
		}

		public IEnumerable<TGenome> AttemptNewCrossover(TGenome primary, TGenome[] others, byte maxAttemptsPerCombination = 3)
		{
			if (primary == null)
				throw new ArgumentNullException(nameof(primary));
			if (others == null)
				throw new ArgumentNullException(nameof(others));
			if (others.Length == 0)
				return null;// throw new InvalidOperationException("Must have at least two unique genomes to crossover with.");
			if (others.Length == 1 && primary != others[0]) return AttemptNewCrossover(primary, others[0], maxAttemptsPerCombination);
			var source = others.Where(g => g != primary).ToArray();
			if (source.Length == 0) return Enumerable.Empty<TGenome>();

			IEnumerable<TGenome> rest()
			{
				// Any left?
				while (source.Length != 0)
				{
					var b = source.RandomSelectOne();
					foreach (var g in AttemptNewCrossover(primary, b, maxAttemptsPerCombination))
						yield return g;

					// Reduce the possibilites.
					source = source.Where(g => g != b).ToArray();
					/* ^^^ Why are we filtering like this you might ask? 
						   Because the source can have duplicates in order to bias randomness. */
				}
			}

			return rest();
		}

		public virtual IEnumerable<TGenome> Expand(TGenome genome, IEnumerable<TGenome> others = null)
		{
			if (others != null)
			{
				foreach (var o in others)
					yield return o;
			}

			//var variation = (TGenome)genome.NextVariation();
			//if (variation != null) yield return AssertFrozen(variation);
			Debug.Assert(genome.Hash.Length != 0, "Cannot expand an empty genome.");
			var mutation = GenerateOne(genome);
			if (mutation != null) yield return mutation;
		}
	}
}
