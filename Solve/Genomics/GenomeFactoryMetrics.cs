using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Filtering;
using App.Metrics.Filters;
using Solve.Metrics;
using System.Collections.Frozen;
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

	internal GenomeFactoryMetrics(MetricsContextValueSource? context)
	{
		static string GetName(CounterValueSource c) => c.Name;
		static CounterValue GetValue(CounterValueSource c) => c.Value;

		FrozenDictionary<string, CounterValue> counters
			= context?.Counters.ToFrozenDictionary(GetName, GetValue)
			?? FrozenDictionary<string, CounterValue>.Empty;

		Timestamp = DateTime.Now;
		ImmutableArray<QueueCount>.Builder queueStates = ImmutableArray.CreateBuilder<QueueCount>();

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
			=> counters.TryGetValue(key, out CounterValue value) ? value.Count : 0;

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

	internal sealed class Logger : CounterCollection
	{
		private const string EXTERNAL_PRODUCER_QUERIED = "External Producer Queried";

		internal Logger(IProvideCounterMetrics metrics)
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

	private static readonly IFilterMetrics MetricsFilter = new MetricsFilter().WhereContext(Context);

	public static GenomeFactoryMetrics Get(IProvideMetricValues? snapshot)
	{
		MetricsContextValueSource? context = snapshot?.Get(MetricsFilter).Contexts.FirstOrDefault();
		return Get(context);
	}

	public static GenomeFactoryMetrics Get(MetricsContextValueSource? snapshot) => new(snapshot);
}
