﻿/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using Open.Collections;
using Open.Numeric;
using Open.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Solve
{

	public abstract class GenomeFactoryBase<TGenome> : IGenomeFactory<TGenome>
		where TGenome : class, IGenome
	{
		protected GenomeFactoryBase()
		{
			Registry = new ConcurrentDictionary<string, Lazy<TGenome>>();
			PreviouslyProduced = new ConcurrentHashSet<string>();
			//RegistryOrder = new ConcurrentQueue<string>();

			HighPriority = new ConcurrentQueue<TGenome>();
			Unexpanded = new ConcurrentQueue<TGenome>();

			PriorityQueues = CreatePriorityQueues().AsReadOnly();
		}

		/**
		 * It's very important to avoid any contention.
		 * 
		 * In order to do so we use a concurrent queue (fast).
		 * Duplicates can occur, but if they are duplicated, we consolodate those duplicates until a valid mate is found, or not.
		 * Returning any valid breeders whom haven't mated enough.
		 */
		readonly ConcurrentQueue<(TGenome Genome, int Count)> BreedingStock
			= new ConcurrentQueue<(TGenome Genome, int Count)>();

		public void EnqueueForBreeding(TGenome genome, int count = 1)
		{
			if (count > 0)
				BreedingStock.Enqueue((genome, count));
		}

		public void EnqueueForBreeding((TGenome genome, int count) breeder)
		{
			if (breeder.count > 0)
				BreedingStock.Enqueue(breeder);
		}

		public void Breed(params TGenome[] genomes)
		{
			if (genomes.Length == 0)
				BreedOne(null);
			else
				foreach (var g in genomes)
					BreedOne(g);
		}

		protected void BreedOne(TGenome genome)
		{
			// Setup incomming...
			(TGenome genome, int count) current;

			if (genome != null)
				current = (genome, 1);
			else if (!BreedingStock.TryDequeue(out current))
				return;

			// Start dequeueing possbile mates, where any of them could be a requeue of current.
			while (BreedingStock.TryDequeue(out (TGenome genome, int count) mate))
			{
				var mateGenome = mate.genome;
				if (mateGenome == genome || mateGenome.Hash == genome.Hash)
				{
					// A repeat of the current?  Increment breeding count and try again.
					current.count++;
				}
				else
				{
					// We have a valid mate!
					EnqueueHighPriority(AttemptNewCrossover(genome, mateGenome));

					// After breeding, decrease their counts.
					current.count--;
					mate.count--;

					// Might still need more funtime.
					EnqueueForBreeding(mate);

					break;
				}
			}

			// Might still need more funtime.
			EnqueueForBreeding(current);
		}

		protected readonly ConcurrentQueue<TGenome> HighPriority;
		protected readonly ConcurrentQueue<TGenome> Unexpanded;
		protected readonly IReadOnlyList<IEnumerable<TGenome>> PriorityQueues;

		protected virtual List<IEnumerable<TGenome>> CreatePriorityQueues()
			=> new List<IEnumerable<TGenome>>()
			{
				HighPriority.AsDequeueingEnumerable(),
				Unexpanded.AsDequeueingEnumerable().SelectMany(g => Expand(g)),
				Generate()
			};

		public void EnqueueHighPriority(params TGenome[] genomes)
		{
			if (genomes == null) return;
			foreach (var g in genomes)
				if(g!=null) HighPriority.Enqueue(g);
		}

		public void EnqueueForExpansion(params TGenome[] genomes)
		{
			if (genomes == null) return;
			foreach (var g in genomes)
				if (g != null) Unexpanded.Enqueue(g);
		}

		// Help to reduce copies.
		// Use a Lazy to enforce one time only execution since ConcurrentDictionary is optimistic.
		protected readonly ConcurrentDictionary<string, Lazy<TGenome>> Registry;

		protected readonly ConcurrentHashSet<string> PreviouslyProduced;

		//protected readonly ConcurrentQueue<string> RegistryOrder;

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
									  //RegistryOrder.Enqueue(hash);
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
									  //RegistryOrder.Enqueue(hash);
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

		protected IEnumerable<TGenome> FilterRegisterNew(IEnumerable<TGenome> source)
		{
			foreach (var e in source)
			{
				var g = Registration(e);
				if (RegisterProduction(g))
					yield return g;
			}
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

		//public string[] GetAllPreviousGenomesInOrder()
		//{

		//	return RegistryOrder.ToArray();
		//}

		// Be sure to call Registration within the GenerateNew call.
		protected abstract TGenome GenerateOneInternal();

		public bool GenerateNew(out TGenome potentiallyNew, params TGenome[] source)
		{
			using (TimeoutHandler.New(5000, ms =>
			{
				Console.WriteLine("Warning: {0}.GenerateOneInternal() is taking longer than {1} milliseconds.\n", this, ms);
			}))
			{
				// Note: for now, we will only mutate by 1.

				// See if it's possible to mutate from the provided genomes.
				if (source != null && source.Length != 0 && AttemptNewMutation(source, out potentiallyNew))
					return true;

				potentiallyNew = Registration(GenerateOneInternal());
			}

			Debug.Assert(potentiallyNew != null, "Converged? No solutions? Saturated?");
			// if(genome==null)
			// 	throw "Failed... Converged? No solutions? Saturated?";

			return potentiallyNew != null && RegisterProduction(potentiallyNew);
		}

		public TGenome GenerateOne(params TGenome[] source)
			=> GenerateNew(out TGenome one, source) ? one : one;

		public IEnumerable<TGenome> GenerateNew(params TGenome[] source)
		{
			while (true)
			{
				TGenome one = null;
				using (TimeoutHandler.New(9000, ms =>
				{
					Console.WriteLine("Warning: {0}.GenerateNew() is taking longer than {1} milliseconds.\n", this, ms);
				}))
				{
					int attempts = 0;
					while (attempts < 100 && !GenerateNew(out one, source))
						attempts++;
				}
				if (one == null)
				{
					Console.WriteLine("GenomeFactory failed GenerateNew()");
					break;
				}

				yield return one;
			}
		}

		public IEnumerable<TGenome> Generate(params TGenome[] source)
		{
			while (true)
			{
				TGenome one;
				using (TimeoutHandler.New(9000, ms =>
				{
					Console.WriteLine("Warning: {0}.GenerateOne() is taking longer than {1} milliseconds.\n", this, ms);
				}))
				{
					one = GenerateOne(source);
				}
				if (one == null)
				{
					Console.WriteLine("GenomeFactory failed GenerateOne()");
					break;
				}
				yield return one;
			}
		}


		// Be sure to call Registration within the GenerateOne call.
		protected abstract TGenome MutateInternal(TGenome target);

		public bool AttemptNewMutation(TGenome source, out TGenome mutation, byte triesPerMutationLevel = 5, byte maxMutations = 3)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			return AttemptNewMutation(new TGenome[] { source }, out mutation, triesPerMutationLevel, maxMutations);
		}

		public bool AttemptNewMutation(TGenome[] source, out TGenome genome, byte triesPerMutationLevel = 5, byte maxMutations = 3)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			Debug.Assert(source.Length != 0, "Should never pass an empty source for mutation.");
			source = source.Where(g => g.Hash.Length != 0).ToArray();
			if (source.Length != 0)
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


		public IEnumerable<TGenome> Mutate(TGenome source)
		{
			while (AttemptNewMutation(source, out TGenome next))
			{
				yield return next;
			}
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


		protected abstract TGenome[] CrossoverInternal(TGenome a, TGenome b);

		protected TGenome[] Crossover(TGenome a, TGenome b)
		{
			var result = CrossoverInternal(a, b);
			if (result != null)
			{
				foreach (var r in result)
				{
					if (r.Hash.Length == 0)
						throw new InvalidOperationException("Cannot process a genome with an empty hash.");
					Registration(r);
				}
			}
			return result;
		}

		public virtual TGenome[] AttemptNewCrossover(TGenome a, TGenome b, byte maxAttempts = 3)
		{
			if (a == null || b == null) return null;

			// Avoid inbreeding. :P
			if (a == b) return null;

			while (maxAttempts != 0)
			{
				var offspring = Crossover(a, b)?.Where(g => RegisterProduction(g)).ToArray();
				if (offspring != null && offspring.Length != 0) return offspring;
				--maxAttempts;
			}

			return null;
		}

		// Random matchmaking...  It's possible to include repeats in the source to improve their chances. Possile O(n!) operaion.
		public TGenome[] AttemptNewCrossover(TGenome[] source, byte maxAttemptsPerCombination = 3)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if (source.Length == 2 && source[0] != source[1]) return AttemptNewCrossover(source[0], source[1], maxAttemptsPerCombination);
			if (source.Length <= 2)
				return null; //throw new InvalidOperationException("Must have at least two unique genomes to crossover with.");

			bool isFirst = true;
			do
			{
				// Take one.
				var a = RandomUtilities.RandomSelectOne(source);
				// Get all others (in orignal order/duplicates).
				var s1 = source.Where(g => g != a).ToArray();

				// Any left?
				while (s1.Length != 0)
				{
					isFirst = false;
					var b = s1.RandomSelectOne();
					var offspring = AttemptNewCrossover(a, b, maxAttemptsPerCombination);
					if (offspring != null && offspring.Length != 0) return offspring;
					// Reduce the possibilites.
					s1 = s1.Where(g => g != b).ToArray();
				}

				if (isFirst) // There were no other available candicates to cross over with. :(
					return null; //throw new InvalidOperationException("Must have at least two unique genomes to crossover with.");

				// Okay so we've been through all of them with 'a' Now move on to another.
				source = source.Where(g => g != a).ToArray();
			}
			while (source.Length > 1); // Less than 2 left? Then we have no other options.

			return null;
		}

		public TGenome[] AttemptNewCrossover(TGenome primary, TGenome[] others, byte maxAttemptsPerCombination = 3)
		{
			if (primary == null)
				throw new ArgumentNullException(nameof(primary));
			if (others == null)
				throw new ArgumentNullException(nameof(others));
			if (others.Length == 0)
				return null;// throw new InvalidOperationException("Must have at least two unique genomes to crossover with.");
			if (others.Length == 1 && primary != others[0]) return AttemptNewCrossover(primary, others[0], maxAttemptsPerCombination);
			var source = others.Where(g => g != primary).ToArray();

			// Any left?
			while (source.Length != 0)
			{
				var b = source.RandomSelectOne();
				var offspring = AttemptNewCrossover(primary, b, maxAttemptsPerCombination);
				if (offspring != null && offspring.Length != 0) return offspring;
				// Reduce the possibilites.
				source = source.Where(g => g != b).ToArray();
				/* ^^^ Why are we filtering like this you might ask? 
					   Because the source can have duplicates in order to bias randomness. */
			}

			return null;
		}

		public virtual IEnumerable<TGenome> Expand(TGenome genome, IEnumerable<TGenome> others = null)
		{
			if (others != null)
			{
				foreach (var o in others)
				{
					yield return o;
				}
			}

			//var variation = (TGenome)genome.NextVariation();
			//if (variation != null) yield return AssertFrozen(variation);
			Debug.Assert(genome.Hash.Length != 0, "Cannot expand an empty genome.");
			if (GenerateNew(out TGenome mutation, genome))
				yield return mutation;
		}

		public virtual IEnumerator<TGenome> GetEnumerator()
		{
			bool found;
			do
			{
				found = false;
				foreach (var e in PriorityQueues)
				{
					foreach (var g in e)
					{
						yield return g;
						found = true;
						break;
					}
					if (found) break;
				}
			}
			while (found);
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
