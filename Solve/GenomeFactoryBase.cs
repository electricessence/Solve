/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using App.Metrics;
using Open.Collections;
using Open.Numeric;
using Open.Threading.Tasks;
using Solve.Metrics;
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

		public readonly IMetricsRoot Metrics;
		internal readonly CounterCollection MetricsCounter;

		protected GenomeFactoryBase(IEnumerable<TGenome> seeds = null)
		{
			Metrics = new MetricsBuilder().Build();
			MetricsCounter = new CounterCollection(Metrics);

			var s = seeds as TGenome[] ?? seeds?.ToArray();
			if (s != null && s.Length != 0) GetPriorityQueue(0).EnqueueInternal(s);
		}

		const string BREEDING_STOCK = "BreedingStock";
		const string INTERNAL_QUEUE_COUNT = "InternalQueue.Count";
		const string AWAITING_VARIATION = "AwaitingVariation";
		const string AWAITING_MUTATION = "AwaitingMutation";



		// Help to reduce copies.
		// Use a Lazy to enforce one time only execution since ConcurrentDictionary is optimistic.
		protected readonly ConcurrentDictionary<string, Lazy<TGenome>> Registry
			= new ConcurrentDictionary<string, Lazy<TGenome>>();

		protected readonly ConcurrentHashSet<string> PreviouslyProduced
			 = new ConcurrentHashSet<string>();

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

		const string GENERATE_NEW_SUCCESS = "Generate New SUCCEDED";
		const string GENERATE_NEW_FAIL = "Generate New FAILED";

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
				{
					MetricsCounter.Increment(GENERATE_NEW_SUCCESS);
					return true;
				}

				potentiallyNew = Registration(GenerateOneInternal());
			}

			Debug.Assert(potentiallyNew != null, "Converged? No solutions? Saturated?");
			// if(genome==null)
			// 	throw "Failed... Converged? No solutions? Saturated?";

			var generated = potentiallyNew != null && RegisterProduction(potentiallyNew);
			if (generated)
			{
				MetricsCounter.Increment(GENERATE_NEW_SUCCESS);
			}
			else
			{
				MetricsCounter.Increment(GENERATE_NEW_FAIL);
			}
			return generated;
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
						{
							MetricsCounter.Increment("Mutation SUCCEDED");
							return true;
						}
					}
				}
				MetricsCounter.Increment("Mutation FAILED");
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
				if (offspring != null && offspring.Length != 0)
				{
					MetricsCounter.Increment("Crossover SUCCEDED");
					return offspring;
				}
				--maxAttempts;
			}

			MetricsCounter.Increment("Crossover Failed");
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

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public IEnumerator<TGenome> GetEnumerator()
		{
			TGenome next;
			while ((next = Next()) != null)
				yield return next;
		}

		public TGenome Next()
		{
			int q = 0;
			while (q < PriorityQueues.Count)
			{
				if (PriorityQueues[q].TryGetNext(out TGenome genome))
					return genome;
				else
					q++;
			}
			return GenerateOne();
		}

		protected List<PriorityQueue> PriorityQueues
			= new List<PriorityQueue>();

		protected PriorityQueue GetPriorityQueue(int index)
		{
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index), index, "Must be at least zero.");

			if (PriorityQueues.Count <= index)
			{
				lock (PriorityQueues)
				{
					int i;
					while ((i = PriorityQueues.Count) <= index)
					{
						var instance = new PriorityQueue(i, this);
						PriorityQueues.Add(instance);
						Debug.Assert(PriorityQueues[i] == instance);
						if (i == index) return instance;
					}
				}
			}

			return PriorityQueues[index];
		}

		public IGenomeFactoryPriorityQueue<TGenome> this[int index]
			=> GetPriorityQueue(index);

		protected class PriorityQueue : IGenomeFactoryPriorityQueue<TGenome>
		{
			readonly int Index;
			readonly GenomeFactoryBase<TGenome> Factory;

			public PriorityQueue(int index, GenomeFactoryBase<TGenome> factory)
			{
				Index = index;
				Factory = factory ?? throw new ArgumentNullException(nameof(factory));
				ProducerTriggers = new List<Func<bool>>()
				{
					ProcessVariation,
					ProcessBreeder,
					ProcessMutation
				};
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

			public void EnqueueChampion(ReadOnlySpan<TGenome> genomes)
			{
				foreach (var genome in genomes)
					EnqueueChampion(genome);
			}
			public void EnqueueChampion(TGenome genome)
			{
				EnqueueForVariation(genome);
				EnqueueForBreeding(genome);
				EnqueueForMutation(genome);
			}

			public void EnqueueForBreeding(ReadOnlySpan<TGenome> genomes)
			{
				if (genomes != null)
					foreach (var g in genomes)
						EnqueueForBreeding(g);
			}

			protected void EnqueueForBreeding((TGenome genome, int count) breeder, bool incrementMetrics)
			{
				var count = breeder.count;
				if (count > 0)
				{
					if (incrementMetrics) Factory.MetricsCounter.Increment(BREEDING_STOCK, count);
					BreedingStock.Enqueue(breeder);
				}
			}

			public void EnqueueForBreeding(TGenome genome, int count = 1)
			{
				if (count > 0)
					EnqueueForBreeding((genome, count), true);
			}

			public void Breed(ReadOnlySpan<TGenome> genomes)
			{
				foreach (var g in genomes)
					Breed(g);
			}

			public void Breed(TGenome genome = null)
			{
				if (genome != null)
					Factory.MetricsCounter.Increment(BREEDING_STOCK);
				BreedOne(genome);
			}

			protected bool TryTakeBreeder(out (TGenome genome, int count) mate)
			{
				if (BreedingStock.TryDequeue(out mate) && mate.count > 0)
				{
					if (mate.count > 1)
					{
						mate.count--;
						if (mate.count > 0) BreedingStock.Enqueue(mate);
						mate = (mate.genome, 1);
					}
					return true;
				}

				return TryTakeBreederFromNextQueue(out mate);
			}

			bool TryTakeBreederFromNextQueue(out (TGenome genome, int count) mate)
			{
				var nextIndex = Index + 1;
				if (Factory.PriorityQueues.Count > nextIndex)
					return Factory.PriorityQueues[nextIndex].TryTakeBreeder(out mate);

				mate = default;
				return false;
			}

			protected bool BreedOne(TGenome genome)
			{
				// Setup incomming...
				(TGenome genome, int count) current;

				if (genome != null)
				{
					current = (genome, 1);
				}
				else if (TryTakeBreeder(out current))
				{
					genome = current.genome;
				}
				else
				{
					// genome was null and nothing was available to breed with.
					return false;
				}

				int remaining = BreedingStock.Count;
				bool bred = false;
				// Start dequeueing possbile mates, where any of them could be a requeue of current.
				while (TryTakeBreeder(out (TGenome genome, int count) mate))
				{
					var mateGenome = mate.genome;
					if (mateGenome == genome || mateGenome.Hash == genome.Hash)
					{
						// A repeat of the current?  Increment breeding count and try again.
						current.count++;
					}
					else
					{
						void decrementCurrent()
						{
							current.count--;
							Factory.MetricsCounter.Decrement(BREEDING_STOCK);
						}
						void decrementMate()
						{
							mate.count--;
							Factory.MetricsCounter.Decrement(BREEDING_STOCK);
						}

						// We have a valid mate!
						if (EnqueueInternal(Factory.AttemptNewCrossover(genome, mateGenome)))
						{
							bred = true;
							// After breeding, decrease their counts.
							decrementCurrent();
							decrementMate();
						}
						else
						{
							// Breeding failures almost always happen early on when genomes are short in length and have already been introduced.
							if (genome.Length < 4) decrementCurrent();
							if (mate.genome.Length < 4) decrementMate();

							// Generate more (and insert at higher priority) to improve the pool;
							EnqueueInternal(Factory.GenerateOne());
							Factory.GetPriorityQueue(Index + 1).EnqueueInternal(Factory.GenerateOne());
						}

						// Might still need more funtime.
						EnqueueForBreeding(mate, false);

						break;
					}

					// It's possible to get stuck in a loop as entried are returned to the breeding stock.
					// This prevents that potential infinite loop.
					if (--remaining < 1) break;
				}

				// Might still need more funtime.
				EnqueueForBreeding(current, false);
				return bred;
			}

			internal bool EnqueueInternal(TGenome genome)
			{
				if (genome != null)
				{
					Factory.MetricsCounter.Increment(INTERNAL_QUEUE_COUNT);
					InternalQueue.Enqueue(genome);
					return true;
				}
				else
				{
					Debug.Fail("A null geneome was provided.");
				}
				return false;
			}

			internal bool EnqueueInternal(ReadOnlySpan<TGenome> genomes)
			{
				bool added = false;
				foreach (var g in genomes)
				{
					if (EnqueueInternal(g))
						added = true;
				}
				return added;
			}

			public bool AttemptEnqueueVariation(TGenome genome)
			{
				if (genome == null) return false;
				if (genome.RemainingVariations.ConcurrentTryMoveNext(out IGenome v))
				{
					if (v is TGenome t)
					{
						t = Factory.Registration(t);
						if (Factory.RegisterProduction(t))
						{
							EnqueueInternal(t);
							return true;
						}
					}
					else
					{
						Debug.Fail("Genome variation does not match the source type.");
					}
				}
				return false;
			}

			public void EnqueueVariations(TGenome genome)
			{
				if (genome != null) while (AttemptEnqueueVariation(genome)) { }
			}

			public void EnqueueVariations(ReadOnlySpan<TGenome> genomes)
			{
				foreach (var g in genomes)
					EnqueueVariations(g);
			}

			public void EnqueueForVariation(TGenome genome)
			{
				if (genome != null)
				{
					Factory.MetricsCounter.Increment(AWAITING_VARIATION);
					AwaitingVariation.Enqueue(genome);
				}
			}

			public void EnqueueForVariation(ReadOnlySpan<TGenome> genomes)
			{
				foreach (var g in genomes)
					EnqueueVariations(g);
			}

			public void EnqueueForMutation(TGenome genome)
			{
				if (genome != null)
				{
					Factory.MetricsCounter.Increment(AWAITING_MUTATION);
					AwaitingMutation.Enqueue(genome);
				}
			}

			public void EnqueueForMutation(ReadOnlySpan<TGenome> genomes)
			{
				foreach (var g in genomes)
					EnqueueForMutation(g);
			}

			protected readonly ConcurrentQueue<TGenome> InternalQueue
				= new ConcurrentQueue<TGenome>();

			protected readonly ConcurrentQueue<TGenome> AwaitingVariation
				= new ConcurrentQueue<TGenome>();

			protected readonly ConcurrentQueue<TGenome> AwaitingMutation
				= new ConcurrentQueue<TGenome>();

			readonly List<Func<bool>> ProducerTriggers;
			public List<Func<bool>> ExternalProducers { get; private set; } = new List<Func<bool>>();

			bool ProcessVariation()
			{
				while (AwaitingVariation.TryDequeue(out TGenome vGenome))
				{
					Factory.MetricsCounter.Decrement(AWAITING_VARIATION);
					if (AttemptEnqueueVariation(vGenome))
					{
						// Taken one off, now put it back.
						EnqueueForVariation(vGenome);
						return true;
					}
				}
				return false;
			}

			bool ProcessBreeder()
				=> BreedOne(null);

			bool ProcessMutation()
			{
				while (AwaitingMutation.TryDequeue(out TGenome mGenome))
				{
					Factory.MetricsCounter.Decrement(AWAITING_MUTATION);
					if (Factory.AttemptNewMutation(mGenome, out TGenome mutation))
					{
						EnqueueInternal(mutation);
						return true;
					}
				}
				return false;
			}

			public bool TryGetNext(out TGenome genome)
			{
				//int av = AwaitingVariation.Count, am = AwaitingMutation.Count, bs = BreedingStock.Count;
				//if (av > 10000 || am > 10000 || bs > 10000)
				//{
				//	throw new Exception($"AwaitingVariation.Count: {av}\nAwaitingMutation.Count: {am}\nBreedingStock.Count: {bs}");
				//}

				do
				{
					if (InternalQueue.TryDequeue(out genome))
					{
						Factory.MetricsCounter.Decrement(INTERNAL_QUEUE_COUNT);
						return true;
					}
				}
				// Next check for high priority items..
				while (ProducerTriggers.Any(t => t()));

				// Trigger any external producers but still return false for this round.
				// We still want random production to occur every so often.
				ExternalProducers.Any(p => p?.Invoke() ?? false);

				return false;
			}

		}
	}
}
