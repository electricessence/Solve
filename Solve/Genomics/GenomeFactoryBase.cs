/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using Open.Collections;
using Open.Collections.Synchronized;
using Open.Disposable;
using Open.Threading.Tasks;
using Solve.Metrics;
using Solve.Telemetry;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Solve;

public abstract class GenomeFactoryBase<TGenome> : DisposableBase, IGenomeFactory<TGenome>
	where TGenome : class, IGenome
{
	private readonly GenomeFactoryMetrics.Logger Metrics;

	// 15-0016: retained (in addition to the write-only Logger above) so this factory can read
	// its own point-in-time counter snapshot on demand -- see MetricsSnapshot / NoveltySaturation
	// below. No new counting mechanism: this is the same CounterRegistry the constructor already
	// received and wrapped in Metrics for writes.
	private readonly CounterRegistry _counterRegistry;

	// 10-0002: optional seedable randomness source. Only GenomeFactoryBase's own direct
	// subclasses can hand a Random through this constructor parameter; classes separated from
	// GenomeFactoryBase by a non-forwarding intermediate base (e.g. ReducibleGenomeFactoryBase,
	// which does not forward a randomSource parameter) instead assign the protected RandomSource
	// setter from their own constructor body -- see Eater's GenomeFactory and
	// EvalGenomeFactoryBase for that pattern. Either path ends up going through the RandomSource
	// property setter below, so the thread-safety wrapping is applied uniformly.
	protected GenomeFactoryBase(CounterRegistry metrics, IEnumerable<TGenome>? seeds = null, Random? randomSource = null)
	{
		_counterRegistry = metrics ?? throw new ArgumentNullException(nameof(metrics));
		Metrics = new GenomeFactoryMetrics.Logger(_counterRegistry);
		if (randomSource is not null)
			RandomSource = randomSource;

		// 15-0030: solve.factory.registry_size / solve.factory.breeding_stock observable
		// gauges. Pull-based (see EngineInstruments' type-level remarks): each callback is only
		// ever invoked by a listener's own polling pass, so registering here adds no work to
		// any per-generation/per-genome hot path. Registry.Count and MetricsSnapshot.BreedingStock
		// are the same values GetOrCreateCounter/Registry consumers already read elsewhere in
		// this class -- no new counting mechanism.
		EngineInstruments.RegisterRegistrySizeSource(this, () => Registry.Count);
		EngineInstruments.RegisterBreedingStockSource(this, () => MetricsSnapshot.BreedingStock);

		InjectSeeds(seeds);
	}

	/// <summary>
	/// A point-in-time snapshot of this factory's operator-outcome counters (generation,
	/// mutation, crossover -- see <see cref="GenomeFactoryMetrics"/>) plus its queue depths.
	/// Cheap and lock-free on the operator hot paths: it re-reads the existing
	/// <see cref="Solve.Metrics.CounterRegistry"/> aggregation this factory already writes to via
	/// its private <see cref="GenomeFactoryMetrics.Logger"/> (see that registry's own remarks for
	/// how the aggregation works); no additional counting mechanism is introduced.
	/// </summary>
	public GenomeFactoryMetrics MetricsSnapshot
		=> GenomeFactoryMetrics.Get(_counterRegistry.Snapshot());

	/// <summary>
	/// Cumulative fraction of this factory's generation/mutation/crossover attempts that failed
	/// to produce a genuinely novel genome so far -- see
	/// <see cref="GenomeFactoryMetrics.NoveltySaturation"/> for the ratio definition and why it
	/// is cumulative rather than windowed. A rising value is an early-warning sign that the
	/// search has gone sterile, well before champion progress visibly stalls (task 15-0016).
	/// </summary>
	public double NoveltySaturation => MetricsSnapshot.NoveltySaturation;

	private Random _randomSource = System.Random.Shared;

	/// <summary>
	/// The randomness source used for every stochastic decision this factory makes: genome
	/// generation (<see cref="GenerateOneInternal"/>), mutation-point selection
	/// (<see cref="MutateInternal"/>), crossover-point selection (<see cref="CrossoverInternal"/>),
	/// and the default matchmaking logic in <see cref="IGenomeFactory{TGenome}.AttemptNewCrossover(in ReadOnlySpan{TGenome}, byte)"/>.
	/// </summary>
	/// <remarks>
	/// Thread-safety strategy (locking): the default, unseeded value is
	/// <see cref="System.Random.Shared"/>, which .NET already guarantees is safe to call from
	/// any thread concurrently, so it is used as-is with zero extra overhead. Any other
	/// <see cref="Random"/> assigned here -- typically a freshly seeded instance a derived
	/// factory's constructor passes through to make a run reproducible -- is automatically
	/// wrapped in <see cref="SynchronizedRandom"/>, which serializes every draw behind a lock.
	/// This factory's producer/consumer machinery (<see cref="PriorityQueue"/>'s
	/// ProcessBreeder/ProcessMutation/ProcessVariation triggers, plus any external
	/// <c>Parallel.ForEach</c>-driven callers) invokes <see cref="GenerateOneInternal"/>,
	/// <see cref="MutateInternal"/>, and <see cref="CrossoverInternal"/> from multiple threads
	/// concurrently, so without this a shared seeded <see cref="Random"/> instance would corrupt
	/// its internal state under concurrent access. Locking guarantees every draw is valid and
	/// none are lost or duplicated -- but since thread-scheduling order is not itself
	/// deterministic, it does NOT guarantee the same interleaving of draws under real parallel
	/// load. A seed therefore reproduces a bit-identical sequence for single-threaded / fixed
	/// call-order use (e.g. unit tests calling <see cref="IGenomeFactory{TGenome}.TryGenerateNew"/>
	/// in a loop); full determinism of the parallel tower pipeline's thread scheduling is
	/// explicitly out of scope (task 10-0002).
	/// </remarks>
	public Random RandomSource
	{
		get => _randomSource;
		protected set => _randomSource = value is null || ReferenceEquals(value, System.Random.Shared)
			? System.Random.Shared
			: new SynchronizedRandom(value);
	}

	private Action<string> _logWarning = static message => Debug.WriteLine(message);

	/// <summary>
	/// Injectable sink for this factory's non-fatal warnings: the generation/mutation
	/// timeout notices raised by <see cref="TryGenerateNew"/>, <see cref="Mutate(TGenome, byte)"/>, and
	/// the default <see cref="IGenomeFactory{TGenome}.GenerateOneFrom(IReadOnlyList{TGenome})"/>
	/// implementation. Defaults to <see cref="Debug.WriteLine(string)"/> so headless hosts
	/// (unit tests, the benchmark harness) see nothing on the console; an interactive host
	/// that wants these surfaced can assign its own sink here (e.g. routed through its own
	/// logger, or through a console emitter that won't corrupt a cursor-positioned display).
	/// </summary>
	public Action<string> LogWarning
	{
		get => _logWarning;
		set => _logWarning = value ?? throw new ArgumentNullException(nameof(value));
	}

	protected void InjectSeeds(IEnumerable<TGenome>? seeds)
	{
		if (seeds is null) return;
		IReadOnlyCollection<TGenome> s = seeds as IReadOnlyCollection<TGenome> ?? seeds.ToArray();
		if (s.Count == 0) return;

		PriorityQueue q = GetPriorityQueue(0);
		q.EnqueueInternal(s, true);
		q.EnqueueForVariation(s);
	}

	// Help to reduce copies.
	// Use a Lazy to enforce one time only execution since ConcurrentDictionary is optimistic.
	protected readonly ConcurrentDictionary<string, Lazy<TGenome>> Registry = new();

	protected readonly LockSynchronizedHashSet<string> PreviouslyProduced = [];

	//protected readonly ConcurrentQueue<string> RegistryOrder;

	// 25-0024: opt-in age-marker tracking. A genome's age marker is the value of this
	// monotonically increasing counter at the moment it was FIRST registered by this
	// factory (see the Register(...) overloads below, which are the sole assignment
	// point -- every public Registration(...) overload, including subclass overrides
	// such as EvalGenomeFactoryBase's, ultimately funnels through one of the two
	// Register(...) methods). Lower markers are older.
	private long _ageCounter;
	private readonly ConcurrentDictionary<string, long> _ageMarkers = new();

	/// <summary>
	/// Enables per-genome creation-order age-marker assignment during registration (see
	/// <see cref="GetAgeMarker(TGenome)"/> and <see cref="CurrentAgeCounter"/>). Defaults to
	/// <see langword="false"/>: while disabled, <see cref="Register(TGenome, out TGenome, Action{TGenome}?)"/>
	/// and its sibling overload perform zero extra work for this feature -- no counter
	/// increment, no dictionary write, no allocation -- so registration is unaffected and
	/// behaves exactly as it did before this feature existed. A consumer that wants age data
	/// (e.g. <c>TowerScheme{TGenome}.Level</c>'s opt-in age-protection ranking lens, driven by
	/// <see cref="Solve.ProcessingSchemes.SchemeConfig.AgeProtectionWindow"/>) sets this to
	/// <see langword="true"/> before genomes it cares about start flowing through the factory.
	/// </summary>
	public bool AgeTrackingEnabled { get; set; }

	/// <summary>
	/// The current value of the age-marker counter -- the marker that will be assigned to the
	/// next newly-registered genome (see <see cref="GetAgeMarker(TGenome)"/>). Only meaningful
	/// once <see cref="AgeTrackingEnabled"/> has been set; reads as zero until then.
	/// </summary>
	public long CurrentAgeCounter => Interlocked.Read(ref _ageCounter);

	/// <summary>
	/// Returns the creation-order age marker assigned to <paramref name="genome"/> when it was
	/// first registered by this factory, or <see langword="null"/> when
	/// <see cref="AgeTrackingEnabled"/> was <see langword="false"/> at that time (including
	/// always, if it has never been enabled) or the genome was never registered by this
	/// factory. Lower values are older; markers are drawn from a single monotonically
	/// increasing counter, so no two genomes ever share one.
	/// </summary>
	public long? GetAgeMarker(TGenome genome)
	{
		ArgumentNullException.ThrowIfNull(genome);
		return _ageMarkers.TryGetValue(genome.Hash, out long age) ? age : null;
	}

	private void AssignAgeMarkerIfEnabled(TGenome genome)
	{
		if (!AgeTrackingEnabled) return;
		long age = Interlocked.Increment(ref _ageCounter);
		_ageMarkers[genome.Hash] = age;
	}

	protected override void OnDispose()
	{
		Registry.Clear();
		PreviouslyProduced.Clear();
	}

	protected static void AssertFrozen(TGenome genome)
	{
		ArgumentNullException.ThrowIfNull(genome);
		Contract.EndContractBlock();

		if (!genome.IsFrozen)
			throw new InvalidOperationException("Genome is not frozen: " + genome);
	}

	protected bool Register(string genomeHash, Func<TGenome> factory, out TGenome actual, Action<TGenome>? onBeforeAdd = null)
	{
		ArgumentNullException.ThrowIfNull(genomeHash);
		ArgumentNullException.ThrowIfNull(factory);
		Contract.EndContractBlock();

		bool added = false;
		actual = Registry.GetOrAdd(genomeHash, hash => Lazy.Create(() =>
		{
			added = true;
			TGenome? genome = factory();
			Debug.Assert(genome is not null);
			Debug.Assert(genome.Hash == hash);
			onBeforeAdd?.Invoke(genome);
			// Cannot allow registration of an unfrozen genome because it then can be used by another thread.
			AssertFrozen(genome);
			AssignAgeMarkerIfEnabled(genome);
			//RegistryOrder.Enqueue(hash);
			return genome;
		})).Value;

		return added;
	}

	// ReSharper disable once UnusedMethodReturnValue.Global
	protected bool Register(TGenome genome, out TGenome actual, Action<TGenome>? onBeforeAdd = null)
	{
		ArgumentNullException.ThrowIfNull(genome);
		Contract.EndContractBlock();

		bool added = false;
		actual = Registry.GetOrAdd(genome.Hash, hash => Lazy.Create(() =>
		{
			added = true;
			Debug.Assert(genome.Hash == hash);
			onBeforeAdd?.Invoke(genome);
			// Cannot allow registration of an unfrozen genome because it then can be used by another thread.
			AssertFrozen(genome);
			AssignAgeMarkerIfEnabled(genome);
			//RegistryOrder.Enqueue(hash);
			return genome;
		})).Value;

		return added;
	}

	[return: NotNullIfNotNull(nameof(genome))]
	protected TGenome? Registration(TGenome? genome, Action<TGenome>? onBeforeAdd = null)
	{
		if (genome is null) return null;
		Debug.Assert(genome.Hash.Length != 0, "Genome cannot have empty hash.");
		_ = Register(genome, out TGenome? result, t =>
		{
			onBeforeAdd?.Invoke(t);
			t.Freeze();
		});
		return result;
	}

	[SuppressMessage("Roslynator", "RCS1163:Unused parameter")]
	protected TGenome Registration(TGenome genome, (string message, string? data) origin, Action<TGenome>? onBeforeAdd = null)
	{
#if DEBUG
		genome.AddLogEntry("Origin", origin.message, origin.data);
#endif
		return Registration(genome, onBeforeAdd);
	}

	protected TGenome Registration(TGenome genome, string origin, Action<TGenome>? onBeforeAdd = null)
		=> Registration(genome, (origin, null), onBeforeAdd);

	protected bool RegisterProduction(TGenome genome)
	{
		ArgumentNullException.ThrowIfNull(genome);
		string hash = genome.Hash;
		return Registry.ContainsKey(hash)
			? PreviouslyProduced.Add(genome.Hash)
			: throw new InvalidOperationException("Registering for production before genome was in global registry.");
	}

	protected IEnumerable<TGenome> FilterRegisterNew(IEnumerable<TGenome> source)
		=> source.Select(e => Registration(e)).Where(RegisterProduction);

	protected bool AlreadyProduced(string hash)
		=> PreviouslyProduced.Contains(hash ?? throw new ArgumentNullException(nameof(hash)));

	protected bool AlreadyProduced(TGenome genome)
		=> genome is null
		? throw new ArgumentNullException(nameof(genome))
		: AlreadyProduced(genome.Hash);

	//public string[] GetAllPreviousGenomesInOrder()
	//{

	//	return RegistryOrder.ToArray();
	//}

	// Be sure to call Registration within the GenerateNew call.
	protected abstract TGenome? GenerateOneInternal();

	public bool TryGenerateNew([NotNullWhen(true)] out TGenome? potentiallyNew, IReadOnlyList<TGenome>? source = null)
	{
#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
		IGenomeFactory<TGenome> factory = this;
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
#pragma warning restore IDE0079 // Remove unnecessary suppression
		using (TimeoutHandler.New(5000,
			ms => LogWarning($"Warning: {this}.GenerateOneInternal() is taking longer than {ms} milliseconds.\n")))
		{
			// Note: for now, we will only mutate by 1.

			// See if it's possible to mutate from the provided genomes.
			if (source is not null && source.Count != 0)
			{
				if (factory.AttemptNewMutation(source, out potentiallyNew))
				{
					Metrics.GenerateNew(true);
					return true;
				}

				Metrics.GenerateNew(false);
				return false;
			}

			potentiallyNew = Registration(GenerateOneInternal());
			//Debug.WriteLine("Potentially New: " + potentiallyNew?.Hash);
		}

		Debug.WriteLineIf(potentiallyNew is null, "TryGenerateNew: Converged? No solutions? Saturated?");
		// if(genome==null)
		// 	throw "Failed... Converged? No solutions? Saturated?";

		if (potentiallyNew is null || !RegisterProduction(potentiallyNew))
		{
			Metrics.GenerateNew(false);
			return false;
		}

		Metrics.GenerateNew(true);
		return true;
	}

	// Be sure to call Registration within the GenerateOne call.
	protected abstract TGenome? MutateInternal(TGenome target);

	public bool AttemptNewMutation(
		TGenome source,
		[NotNullWhen(true)] out TGenome? mutation,
		byte triesPerMutationLevel = 2,
		byte maxMutations = 3)
	{
		ArgumentNullException.ThrowIfNull(source);
		Debug.Assert(source.Hash.Length != 0);
		if (source.Hash.Length == 0)
		{
			mutation = default!;
			return false;
		}

		// Find one that will mutate well and use it.
		for (byte m = 1; m <= maxMutations; m++)
		{
			for (byte t = 0; t < triesPerMutationLevel; t++)
			{
				mutation = Mutate(source, m);
				if (mutation is null || !RegisterProduction(mutation)) continue;
				Metrics.Mutation(true);
				return true;
			}
		}

		Metrics.Mutation(false);

		mutation = default!;
		return false;
	}

	protected TGenome? Mutate(TGenome source, byte mutations)
	{
		if (mutations == 0) throw new ArgumentOutOfRangeException(nameof(mutations));
		Contract.EndContractBlock();

		TGenome original = source;
		TGenome? genome = null;
		while (mutations != 0)
		{
			byte tries = 3;
			while (tries != 0 && genome is null)
			{
				TGenome s = source;
				void onTimeout(double ms) => LogWarning($"Warning: {this}.MutateInternal({s}) is taking longer than {ms} milliseconds.\n");
				using (TimeoutHandler.New(3000, onTimeout))
				{
					genome = MutateInternal(source);
					string? hash = genome?.Hash;
					if (hash is not null && (hash == source.Hash || hash == original.Hash))
						genome = null; // Not a mutation. Could happen on repeat mutations.
				}

				--tries;
			}
			// Reuse the clone as the source 
			if (genome is null) break; // No single mutation possible? :/
			source = genome;
			--mutations;
		}

		return Registration(genome);
	}

	protected abstract TGenome[] CrossoverInternal(TGenome a, TGenome b);

	// ReSharper disable once ReturnTypeCanBeEnumerable.Global
	protected TGenome[] Crossover(TGenome a, TGenome b)
	{
		TGenome[] result = CrossoverInternal(
			a ?? throw new ArgumentNullException(nameof(a)),
			b ?? throw new ArgumentNullException(nameof(b))
		);

		foreach (TGenome r in result)
		{
			if (r.Hash.Length == 0)
				throw new InvalidOperationException("Cannot process a genome with an empty hash.");
			Registration(r);
		}

		return result;
	}

	protected virtual bool CannotCrossover(TGenome a, TGenome b)
		=> a is null || b is null
			// Avoid inbreeding. :P
			|| a == b
			|| a.Hash == b.Hash;

	public virtual TGenome[] AttemptNewCrossover(TGenome a, TGenome b, byte maxAttempts = 3)
	{
		if (CannotCrossover(a, b))
			return [];

		byte m = maxAttempts;
		while (m != 0)
		{
			TGenome[] offspring = Crossover(a, b).Where(RegisterProduction).ToArray();
			if (offspring.Length != 0)
			{
				Metrics.Crossover(true);
				return offspring;
			}

			--m;
		}

		Metrics.Crossover(false);
		return [];
	}

	// Tracking every released genome in an unbounded dictionary is expensive (unbounded
	// memory growth over a long run) and only useful when actively hunting for duplicate-
	// release / duplicate-production bugs. It compiles in only when GENOME_DIAGNOSTICS is
	// explicitly opted into, not on plain DEBUG.
#if DEBUG && GENOME_DIAGNOSTICS
	private readonly ConcurrentDictionary<string, TGenome> Released = new();
#endif

	public TGenome Next()
	{
#if DEBUG && GENOME_DIAGNOSTICS
		bool generated = false;
		TGenome next()
		{
#endif
			int q = 0;
			while (q < PriorityQueues.Count)
			{
				if (PriorityQueues[q].TryGetNext(out TGenome? genome))
					return genome;
				else
					q++;
			}
#if DEBUG && GENOME_DIAGNOSTICS
			generated = true;
#endif
			return ((IGenomeFactory<TGenome>)this).GenerateOne();
#if DEBUG && GENOME_DIAGNOSTICS
		}

		TGenome n = next();
		string h = n.Hash;
		bool added = Released.TryAdd(h, n);
		if (added)
			return n;

		TGenome actual = Released[h];
		if (actual == n)
		{
			Debug.Assert(added, "This factory is releasing the same genome twice. Generated: " + generated, n.StackTrace);
		}
		else
		{
			Debug.Assert(added, $"This factory is producing a duplicate genome. {h}", $"This Instance:\n{actual.StackTrace}\nOriginal Instance:\n{n.StackTrace}");
		}

		return n;
#endif
	}

	protected readonly Collection<PriorityQueue> PriorityQueues = [];

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

	private readonly ConditionalWeakTable<TGenome, IEnumerator<TGenome>> Variations = [];

	protected virtual IEnumerable<TGenome>? GetVariationsInternal(TGenome source) => null;

	public IEnumerator<TGenome>? GetVariations(TGenome source)
	{
		if (Variations.TryGetValue(source, out IEnumerator<TGenome>? r))
			return r;

		IEnumerable<TGenome>? result = GetVariationsInternal(source);
		return result is null ? null
			: Variations.GetValue(source,
				_ => result.Distinct(GenomeEqualityComparer<TGenome>.Instance).GetEnumerator());
	}

	protected class PriorityQueue : IGenomeFactoryPriorityQueue<TGenome>
	{
		private readonly int Index;
		private readonly GenomeFactoryBase<TGenome> Factory;

		public PriorityQueue(int index, GenomeFactoryBase<TGenome> factory)
		{
			Index = index;
			Factory = factory ?? throw new ArgumentNullException(nameof(factory));
			ProducerTriggers =
			[
				ProcessVariation,
				ProcessBreeder,
				ProcessMutation
			];
		}

		/**
		 * It's very important to avoid any contention.
		 *
		 * In order to do so we use a concurrent queue (fast).
		 * Duplicates can occur, but if they are duplicated, we consolodate those duplicates until a valid mate is found, or not.
		 * Returning any valid breeders whom haven't mated enough.
		 */
		private readonly ConcurrentQueue<(TGenome Genome, int Count)> BreedingStock = new();

		public void EnqueueChampion(IEnumerable<TGenome> genomes)
		{
			foreach (TGenome genome in genomes)
				EnqueueChampion(genome);
		}

		public void EnqueueChampion(TGenome genome)
		{
			EnqueueForVariation(genome);
			EnqueueForBreeding(genome);
			EnqueueForMutation(genome);
		}

		public void EnqueueForBreeding(IEnumerable<TGenome> genomes)
		{
			if (genomes is null) return;
			foreach (TGenome g in genomes)
				EnqueueForBreeding(g);
		}

		protected void EnqueueForBreeding(TGenome genome, int count, bool incrementMetrics)
		{
			if (count > 0)
			{
				if (incrementMetrics) Factory.Metrics.BreedingStock.Increment(count);
				BreedingStock.Enqueue((genome, count));
			}
		}

		[SuppressMessage("Roslynator", "RCS1242:Do not pass non-read-only struct by read-only reference.")]
		protected void EnqueueForBreeding(in (TGenome Genome, int Count) entry, bool incrementMetrics)
		{
			int count = entry.Count;
			if (count > 0)
			{
				if (incrementMetrics) Factory.Metrics.BreedingStock.Increment(count);
				BreedingStock.Enqueue(entry);
			}
		}

		public void EnqueueForBreeding(TGenome genome, int count = 1)
		{
			if (count > 0)
				EnqueueForBreeding(genome, count, true);
		}

		public void Breed(IEnumerable<TGenome> genomes)
		{
			foreach (TGenome g in genomes)
				Breed(g);
		}

		public void Breed(TGenome? genome = null, int maxCount = 1)
		{
			for (int i = 0; i < maxCount; i++)
			{
				if (genome is not null)
					Factory.Metrics.BreedingStock.Increment();
				if (!BreedOne(genome))
					break;
			}
		}

		protected bool TryTakeBreeder(out (TGenome genome, int count) mate)
		{
			if (!BreedingStock.TryDequeue(out mate) || mate.count <= 0)
				return TryTakeBreederFromNextQueue(out mate);

			if (mate.count <= 1) return true;
			mate.count--;
			if (mate.count > 0) BreedingStock.Enqueue(mate);
			mate = (mate.genome, 1);
			return true;
		}

		private bool TryTakeBreederFromNextQueue(out (TGenome genome, int count) mate)
		{
			int nextIndex = Index + 1;
			if (Factory.PriorityQueues.Count > nextIndex)
				return Factory.PriorityQueues[nextIndex].TryTakeBreeder(out mate);

			mate = default;
			return false;
		}

		protected bool BreedOne(TGenome? genome)
		{
			// Setup incomming...
			(TGenome genome, int count) current;

			if (genome is not null)
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
				TGenome mateGenome = mate.genome;
				if (mateGenome == genome || mateGenome.Hash == genome.Hash)
				{
					//if (!EnqueueMutation(genome))
					//{
					// A repeat of the current?  Increment breeding count and try again.
					current.count++;
					//}
				}
				else
				{
					void decrementCurrent()
					{
						current.count--;
						Factory.Metrics.BreedingStock.Decrement();
					}

					void decrementMate()
					{
						mate.count--;
						Factory.Metrics.BreedingStock.Decrement();
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
						// Always decay on failure so persistently-failing entries drain
						// from the stock instead of accumulating without bound. (Failures
						// dominate late in a run when most offspring are duplicates.)
						decrementCurrent();
						decrementMate();

						// Generate more (and insert at higher priority) to improve the pool.
						// This can be problematic later on.
						//var g = ((IGenomeFactory<TGenome>)Factory).GenerateOneFrom(genome, mate.genome);
						//if (g is not null) EnqueueInternal(g);
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

		public void Inject(TGenome genome)
			=> EnqueueInternal(genome, true);

		public void Inject(IEnumerable<TGenome> genomes)
			=> EnqueueInternal(genomes as IReadOnlyList<TGenome> ?? genomes.ToArray(), true);

		internal bool EnqueueInternal(TGenome genome, bool onlyIfNotRegistered = false)
		{
			if (genome is not null)
			{
				if (onlyIfNotRegistered)
				{
					genome = Factory.Registration(genome);
					if (!Factory.RegisterProduction(genome))
						return false;
				}
#if DEBUG
				else
				{
					Debug.Assert(Factory.AlreadyProduced(genome));
				}
#endif

				Factory.Metrics.InternalQueueCount.Increment();
				InternalQueue.Enqueue(genome);
				return true;
			}
			else
			{
				Debug.Fail("A null genome was provided.");
			}

			return false;
		}

		internal bool EnqueueInternal(IEnumerable<TGenome> genomes, bool onlyIfNotRegistered = false)
		{
			bool added = false;
			foreach (TGenome g in genomes)
			{
				if (EnqueueInternal(g, onlyIfNotRegistered))
					added = true;
			}

			return added;
		}

		public bool AttemptEnqueueVariation(TGenome genome)
		{
			if (genome is null) return false;

			IEnumerator<TGenome>? variations = Factory.GetVariations(genome);
			if (variations is null) return false;

			while (variations.ConcurrentTryMoveNext(out IGenome v))
			{
				if (v is TGenome t)
				{
					if (v != genome && v.Hash != genome.Hash && EnqueueInternal(t, true))
						return true;
				}
				else
				{
					Debug.Fail("Genome variation does not match the source type.");
				}
			}

			return false;
		}

		public void EnqueueVariations(TGenome genome, int count = int.MaxValue)
		{
			if (genome is null) return;
			int i = 0;
			while (AttemptEnqueueVariation(genome) && i++ < count) { }
		}

		public void EnqueueVariations(IEnumerable<TGenome> genomes, int count = int.MaxValue)
			=> Parallel.ForEach(genomes, g => EnqueueVariations(g, count));

		public void EnqueueForVariation(TGenome genome)
		{
			if (genome is not null)
			{
				Factory.Metrics.AwaitingVariation.Increment();
				AwaitingVariation.Enqueue(genome);
			}
		}

		public void EnqueueForVariation(IEnumerable<TGenome> genomes)
		{
			foreach (TGenome g in genomes)
				EnqueueForVariation(g);
		}

		public bool Mutate(TGenome genome, int maxCount = 1)
		{
			if (maxCount < 1) return false;
			int i = 0;
			for (; i < maxCount; i++)
			{
				if (Factory.AttemptNewMutation(genome, out TGenome? mutation))
				{
					EnqueueInternal(mutation);
				}
				else
				{
					break;
				}
			}

			return i != 0;
		}

		public void EnqueueForMutation(TGenome genome, int count = 1)
		{
			if (genome is not null)
			{
				Factory.Metrics.AwaitingMutation.Increment();
				AwaitingMutation.Enqueue(genome);
			}
		}

		public void EnqueueForMutation(IEnumerable<TGenome> genomes)
		{
			foreach (TGenome g in genomes)
				EnqueueForMutation(g);
		}

		protected readonly ConcurrentQueue<TGenome> InternalQueue = new();
		protected readonly ConcurrentQueue<TGenome> AwaitingVariation = new();
		protected readonly ConcurrentQueue<TGenome> AwaitingMutation = new();
		private readonly List<Func<bool>> ProducerTriggers;

		public Collection<Func<bool>> ExternalProducers { get; } = [];

		private bool ProcessVariation()
		{
			while (AwaitingVariation.TryDequeue(out TGenome? vGenome))
			{
				Factory.Metrics.AwaitingVariation.Decrement();
				if (!AttemptEnqueueVariation(vGenome)) continue;
				// Taken one off, now put it back.
				EnqueueForVariation(vGenome);
				return true;
			}

			return false;
		}

		private bool ProcessBreeder()
		{
			int count = BreedingStock.Count;
			// Cap work per call: the previous formula was cubic in stock size and
			// could stall Next() for minutes once breeding success collapsed.
			int c = Math.Min(count, 64);
			bool bred = false;
			for (int i = 0; i < c; i++)
			{
				if (BreedOne(null))
					bred = true;
			}

			return bred;
		}

		private bool ProcessMutation()
		{
			while (AwaitingMutation.TryDequeue(out TGenome? mGenome))
			{
				Factory.Metrics.AwaitingMutation.Decrement();
				if (!Factory.AttemptNewMutation(mGenome, out TGenome? mutation))
					continue;
				EnqueueInternal(mutation);
				return true;
			}

			return false;
		}

		public bool TryGetNext([NotNullWhen(true)] out TGenome? genome)
		{
			//int av = AwaitingVariation.Count, am = AwaitingMutation.Count, bs = BreedingStock.Count;
			//if (av > 10000 || am > 10000 || bs > 10000)
			//{
			//	throw new Exception($"AwaitingVariation.Count: {av}\nAwaitingMutation.Count: {am}\nBreedingStock.Count: {bs}");
			//}

			do
			{
				if (!InternalQueue.TryDequeue(out genome))
					continue;
				Factory.Metrics.InternalQueueCount.Decrement();
				return true;
			}
			// Next check for high priority items..
			while (ProducerTriggers.Any(t => t()));

			if (ExternalProducers.Count == 0) return false;

			// Trigger any external producers but still return false for this round.
			// We still want random production to occur every so often.

			// ReSharper disable once ReturnValueOfPureMethodIsNotUsed
			_ = ExternalProducers.Any(p => p.Invoke());
			Factory.Metrics.ExternalProducer();

			return false;
		}
	}
}

/// <summary>
/// Wraps a <see cref="Random"/> instance so every draw is serialized behind a lock, making it
/// safe to share one seeded generator across concurrent callers (see
/// <see cref="GenomeFactoryBase{TGenome}.RandomSource"/> for why this is needed and what
/// determinism guarantee it does -- and does not -- provide under real parallelism).
/// Every public virtual member of <see cref="Random"/> is overridden explicitly (rather than
/// relying on the base class routing everything through <see cref="Sample"/>) so behavior does
/// not depend on undocumented internal dispatch details of the wrapped instance.
/// </summary>
internal sealed class SynchronizedRandom(Random source) : Random
{
	private readonly Random _source = source ?? throw new ArgumentNullException(nameof(source));
	private readonly System.Threading.Lock _gate = new();

	public override int Next()
	{
		lock (_gate) return _source.Next();
	}

	public override int Next(int maxValue)
	{
		lock (_gate) return _source.Next(maxValue);
	}

	public override int Next(int minValue, int maxValue)
	{
		lock (_gate) return _source.Next(minValue, maxValue);
	}

	public override long NextInt64()
	{
		lock (_gate) return _source.NextInt64();
	}

	public override long NextInt64(long maxValue)
	{
		lock (_gate) return _source.NextInt64(maxValue);
	}

	public override long NextInt64(long minValue, long maxValue)
	{
		lock (_gate) return _source.NextInt64(minValue, maxValue);
	}

	public override double NextDouble()
	{
		lock (_gate) return _source.NextDouble();
	}

	public override float NextSingle()
	{
		lock (_gate) return _source.NextSingle();
	}

	public override void NextBytes(byte[] buffer)
	{
		lock (_gate) _source.NextBytes(buffer);
	}

	public override void NextBytes(Span<byte> buffer)
	{
		lock (_gate) _source.NextBytes(buffer);
	}

	protected override double Sample()
	{
		lock (_gate) return _source.NextDouble();
	}
}
