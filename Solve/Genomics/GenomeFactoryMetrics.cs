using Solve.Metrics;
using System.Collections.Immutable;

namespace Solve;

public readonly record struct GenomeFactoryMetrics : IGenomeFactoryMetrics
{
	public const string Context = "GenomeFactory";
	private const string BREEDING_STOCK = "Breeding Stock";
	private const string INTERNAL_QUEUE_COUNT = "InternalQueue Count";
	private const string AWAITING_VARIATION = "Awaiting Variation";
	private const string AWAITING_MUTATION = "Awaiting Mutation";
	private static readonly SuccessFailKeys GENERATE_NEW = "Generate New";
	private static readonly SuccessFailKeys MUTATION = "Mutation";
	private static readonly SuccessFailKeys CROSSOVER = "Crossover";
	private const string EXTERNAL_PRODUCER_QUERIED = "External Producer Queried";

	internal GenomeFactoryMetrics(IMetricsSnapshot? snapshot)
	{
		Timestamp = DateTime.Now;
		// Capacity fixed at 4 (one per AddQueueState call below): the parameterless
		// CreateBuilder() defaults to capacity 8, which would leave Count (4) != Capacity
		// (8) and make MoveToImmutable() below throw -- this path was never exercised by
		// the test suite (nothing calls GenomeFactoryMetrics.Get with a real snapshot), so
		// the pre-existing mismatch was never caught.
		ImmutableArray<QueueCount>.Builder queueStates = ImmutableArray.CreateBuilder<QueueCount>(4);

		BreedingStock = AddQueueState(BREEDING_STOCK);
		InternalQueueCount = AddQueueState(INTERNAL_QUEUE_COUNT);
		AwaitingVariation = AddQueueState(AWAITING_VARIATION);
		AwaitingMutation = AddQueueState(AWAITING_MUTATION);

		GenerateNew = GetSuccessFail(GENERATE_NEW);
		Mutation = GetSuccessFail(MUTATION);
		Crossover = GetSuccessFail(CROSSOVER);

		ExternalProducerQueried = GetCount(EXTERNAL_PRODUCER_QUERIED);

		QueueStates = queueStates.MoveToImmutable();

		long AddQueueState(string key)
		{
			long value = GetCount(key);
			queueStates.Add(new QueueCount(key, value));
			return value;
		}

		long GetCount(string key)
			=> snapshot?.GetValue(Context, key) ?? 0;

		SuccessFailCount GetSuccessFail(SuccessFailKeys key)
			=> new(GetCount(key.Succeded), GetCount(key.Failed));
	}

	public ImmutableArray<QueueCount> QueueStates { get; }

	public DateTime Timestamp { get; }

	public long BreedingStock { get; }

	public long InternalQueueCount { get; }

	public long AwaitingVariation { get; }

	public long AwaitingMutation { get; }

	public SuccessFailCount GenerateNew { get; }

	public SuccessFailCount Mutation { get; }

	public SuccessFailCount Crossover { get; }

	public long ExternalProducerQueried { get; }

	// 15-0016: novelty-saturation visibility. When most generation/mutation/crossover
	// attempts collide with an already-registered genome (see
	// GenomeFactoryBase<TGenome>.RegisterProduction), the search has gone sterile long before
	// champion progress visibly stalls. This ratio surfaces that trend early.
	//
	// Deliberately CUMULATIVE (lifetime failures / lifetime attempts), not windowed: the
	// counters it reads (GenerateNew, Mutation, Crossover) are themselves cumulative running
	// totals maintained by CounterRegistry (see its remarks -- System.Diagnostics.Metrics is
	// emit-only, so CounterRegistry's MeterListener aggregates every measurement into one
	// running total per instrument, with no history retained). A true sliding-window ratio
	// would need new state (e.g. a ring buffer of recent outcomes) and, given the factory's
	// producer/consumer machinery increments these counters from multiple threads
	// concurrently, likely new synchronization on the operator hot paths -- both out of scope
	// here (see task 15-0016's scope boundary: no new locks, read existing snapshots only).
	// A cumulative ratio still serves the early-warning purpose: early in a run it is noisy
	// (few attempts), but as a run progresses it converges toward the current failure rate and
	// a sustained rise remains clearly visible to an operator watching successive snapshots.
	/// <summary>
	/// Cumulative fraction of generation/mutation/crossover attempts (successes + failures,
	/// since this factory was created) that failed to produce a genuinely novel genome. Ranges
	/// from 0 (nothing attempted yet, or every attempt so far has succeeded) to 1 (every
	/// attempt so far has collided with an already-produced genome). See the remarks above for
	/// why this is a cumulative rather than windowed ratio.
	/// </summary>
	public double NoveltySaturation
	{
		get
		{
			long succeeded = GenerateNew.Succeeded + Mutation.Succeeded + Crossover.Succeeded;
			long failed = GenerateNew.Failed + Mutation.Failed + Crossover.Failed;
			long attempts = succeeded + failed;
			return attempts == 0 ? 0d : (double)failed / attempts;
		}
	}

	internal sealed class Logger : CounterCollection
	{
		private const string EXTERNAL_PRODUCER_QUERIED = "External Producer Queried";

		internal Logger(CounterRegistry metrics)
			: base(metrics, GenomeFactoryMetrics.Context)
		{
		}

		public ICounter BreedingStock => this[BREEDING_STOCK];
		public ICounter InternalQueueCount => this[INTERNAL_QUEUE_COUNT];
		public ICounter AwaitingVariation => this[AWAITING_VARIATION];
		public ICounter AwaitingMutation => this[AWAITING_MUTATION];

		public void GenerateNew(bool success)
			=> this[GENERATE_NEW.Switch(success)].Increment();

		public void Mutation(bool success)
			=> this[MUTATION.Switch(success)].Increment();

		public void Crossover(bool success)
			=> this[CROSSOVER.Switch(success)].Increment();

		public void ExternalProducer()
			=> this[EXTERNAL_PRODUCER_QUERIED].Increment();
	}

	public static GenomeFactoryMetrics Get(IMetricsSnapshot? snapshot) => new(snapshot);
}
