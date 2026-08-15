using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

namespace Solve.Telemetry;

/// <summary>
/// Task 15-0030's richer <see cref="System.Diagnostics.Metrics"/> instrument taxonomy for the
/// engine: an evaluations counter, an evaluation-duration histogram, a champion-gene-count
/// histogram, and observable gauges for tower level count, breeding-stock depth, and genome
/// registry size. See <c>Solve/Telemetry/InstrumentTaxonomy.md</c> for the human-readable table
/// (name, kind, unit, description) this class implements.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Solve.Metrics.CounterRegistry"/> (task 25-0025) already owns a <see cref="Meter"/>,
/// but it is a private, per-instance field: every <c>new CounterRegistry()</c> call (the engine's
/// own convention -- see every constructor site under <c>Solve</c>/<c>Problems</c>) creates its
/// own <see cref="Meter"/> named <c>Solve.Metrics.{Guid}</c> unless a caller supplies a stable
/// name, and that Meter is never exposed for other code to add instruments to. This class follows
/// the same <c>Solve.Metrics.*</c> naming root -- so a reader of both sees they belong to the same
/// family -- but under one deliberately fixed, well-known name (<see cref="MeterName"/>) rather
/// than a per-instance random one, specifically so an external tool (<c>dotnet-counters monitor
/// --counters Solve.Metrics.Engine</c>, or an OpenTelemetry <c>MeterProvider.AddMeter(...)</c>)
/// can address it without first having to discover a randomized name at runtime. It is a second
/// <see cref="Meter"/> object, but not a second, unrelated one: it is process-wide (one instance
/// for the whole engine, regardless of how many <see cref="Solve.Metrics.CounterRegistry"/>,
/// factory, or scheme instances exist) precisely because <see cref="Solve.Metrics.CounterRegistry"/>'s
/// per-instance design cannot serve that "one well-known name to monitor" role itself.
/// </para>
/// <para>
/// Counter/histogram recording (<see cref="RecordEvaluation"/>, <see cref="RecordChampionGeneCount"/>)
/// calls straight into the <see cref="Meter"/> API with no additional locking -- the same
/// "emit-only, no aggregation on the write path" shape <see cref="Solve.Metrics.CounterRegistry"/>
/// itself relies on -- so these stay safe to call from the engine's hot paths (every
/// <see cref="ProblemBase{TGenome}"/> evaluation; every champion broadcast).
/// </para>
/// <para>
/// The observable gauges are pull-, not push-based: <see cref="RegisterLevelCountSource"/>,
/// <see cref="RegisterBreedingStockSource"/>, and <see cref="RegisterRegistrySizeSource"/> each
/// register a cheap read callback once per owning instance (a <c>TowerScheme{TGenome}.ProblemTower</c>
/// or <c>GenomeFactoryBase{TGenome}</c>) in a <see cref="ConditionalWeakTable{TKey,TValue}"/> keyed
/// by that owner -- no per-generation/per-evaluation write ever touches this class. The callback
/// itself only runs when a listener (<c>dotnet-counters</c>, an OpenTelemetry
/// <c>PeriodicExportingMetricReader</c>, or a test's <see cref="MeterListener"/>) actually polls,
/// which <see cref="System.Diagnostics.Metrics.ObservableGauge{T}"/> guarantees happens at a
/// bounded, low frequency (typically once per second) rather than on every state change. Because
/// the table only holds a <em>weak</em> reference to each owner, a source is dropped automatically
/// once its owner becomes unreachable -- there is no explicit unregister/dispose call for a caller
/// to remember, and registering here can never keep an otherwise-dead tower or factory alive.
/// </para>
/// </remarks>
public static class EngineInstruments
{
	/// <summary>
	/// The fixed, well-known <see cref="Meter"/> name this class publishes every instrument
	/// under -- pass this to <c>dotnet-counters monitor --counters</c> or an OpenTelemetry
	/// <c>MeterProvider.AddMeter(...)</c> call to observe all of them.
	/// </summary>
	public const string MeterName = "Solve.Metrics.Engine";

	public const string EvaluationsInstrumentName = "solve.evaluations";
	public const string EvaluationDurationInstrumentName = "solve.evaluation.duration";
	public const string ChampionGeneCountInstrumentName = "solve.champion.gene_count";
	public const string LevelCountInstrumentName = "solve.tower.level_count";
	public const string BreedingStockInstrumentName = "solve.factory.breeding_stock";
	public const string RegistrySizeInstrumentName = "solve.factory.registry_size";

	private static readonly Meter _meter = new(MeterName);

	/// <summary>
	/// The shared engine <see cref="Meter"/> every instrument below is published on. Exposed
	/// for tests (and any other in-process code that wants to attach its own
	/// <see cref="MeterListener"/>) rather than requiring every consumer to match on
	/// <see cref="MeterName"/> by string.
	/// </summary>
	public static Meter Meter => _meter;

	private static readonly Counter<long> _evaluations = _meter.CreateCounter<long>(
		EvaluationsInstrumentName,
		unit: "{evaluation}",
		description: "Count of ProblemBase fitness-case evaluations (ProcessSample/ProcessSampleAsync calls), across every problem instance in this process.");

	private static readonly Histogram<double> _evaluationDuration = _meter.CreateHistogram<double>(
		EvaluationDurationInstrumentName,
		unit: "ms",
		description: "Wall-clock duration of a single ProblemBase evaluation (the ProcessSampleMetrics/ProcessSampleMetricsAsync call that produces one fitness sample).");

	private static readonly Histogram<long> _championGeneCount = _meter.CreateHistogram<long>(
		ChampionGeneCountInstrumentName,
		unit: "{gene}",
		description: "Genome.GeneCount of each champion at the moment it is broadcast (TowerScheme<TGenome>.Level.ProcessChampion).");

	/// <summary>
	/// Records one <see cref="ProblemBase{TGenome}"/> evaluation: increments
	/// <see cref="EvaluationsInstrumentName"/> by one and records <paramref name="elapsedMilliseconds"/>
	/// into <see cref="EvaluationDurationInstrumentName"/>. Called from
	/// <see cref="ProblemBase{TGenome}.ProcessSample"/> / <c>ProcessSampleAsync</c> -- the same
	/// call sites that already increment <see cref="ProblemBase{TGenome}.TestCount"/> -- so this
	/// instrument's count always agrees with <c>TestCount</c> summed across every problem.
	/// </summary>
	public static void RecordEvaluation(double elapsedMilliseconds)
	{
		_evaluations.Add(1);
		_evaluationDuration.Record(elapsedMilliseconds);
	}

	/// <summary>
	/// Records <paramref name="geneCount"/> into <see cref="ChampionGeneCountInstrumentName"/>.
	/// Called once per champion broadcast (see <c>TowerScheme{TGenome}.Level.ProcessChampion</c>).
	/// </summary>
	public static void RecordChampionGeneCount(int geneCount)
		=> _championGeneCount.Record(geneCount);

	private static readonly ObservableGaugeSource _levelCountSource = new();
	private static readonly ObservableGaugeSource _breedingStockSource = new();
	private static readonly ObservableGaugeSource _registrySizeSource = new();

	private static readonly ObservableGauge<long> _levelCountGauge = _meter.CreateObservableGauge(
		LevelCountInstrumentName,
		_levelCountSource.Observe,
		unit: "{level}",
		description: "Number of tower levels currently created, one measurement per active TowerScheme<TGenome>.ProblemTower.");

	private static readonly ObservableGauge<long> _breedingStockGauge = _meter.CreateObservableGauge(
		BreedingStockInstrumentName,
		_breedingStockSource.Observe,
		unit: "{genome}",
		description: "Current breeding-stock depth, one measurement per active GenomeFactoryBase<TGenome>.");

	private static readonly ObservableGauge<long> _registrySizeGauge = _meter.CreateObservableGauge(
		RegistrySizeInstrumentName,
		_registrySizeSource.Observe,
		unit: "{genome}",
		description: "Current genome registry size, one measurement per active GenomeFactoryBase<TGenome>.");

	/// <summary>
	/// Registers (or replaces) <paramref name="owner"/>'s current-level-count reader for
	/// <see cref="LevelCountInstrumentName"/>. <paramref name="owner"/> is kept only as a weak
	/// key (see the type-level remarks); <paramref name="read"/> must be cheap and safe to call
	/// concurrently with anything else <paramref name="owner"/> is doing, since it may be invoked
	/// from a listener's polling thread at any time.
	/// </summary>
	public static void RegisterLevelCountSource(object owner, Func<long> read)
		=> _levelCountSource.Register(owner, read);

	/// <summary>Registers <paramref name="owner"/>'s breeding-stock-depth reader. See <see cref="RegisterLevelCountSource"/>.</summary>
	public static void RegisterBreedingStockSource(object owner, Func<long> read)
		=> _breedingStockSource.Register(owner, read);

	/// <summary>Registers <paramref name="owner"/>'s registry-size reader. See <see cref="RegisterLevelCountSource"/>.</summary>
	public static void RegisterRegistrySizeSource(object owner, Func<long> read)
		=> _registrySizeSource.Register(owner, read);

	/// <summary>
	/// Backing store for one observable-gauge's set of per-owner read callbacks. See the
	/// type-level remarks on <see cref="EngineInstruments"/> for why this is pull-based and
	/// weakly keyed.
	/// </summary>
	private sealed class ObservableGaugeSource
	{
		private readonly ConditionalWeakTable<object, Func<long>> _sources = new();

		public void Register(object owner, Func<long> read)
			=> _sources.AddOrUpdate(owner, read);

		public IEnumerable<Measurement<long>> Observe()
		{
			foreach (KeyValuePair<object, Func<long>> kv in _sources)
			{
				long value;
				try
				{
					value = kv.Value();
				}
				catch
				{
					// A source callback must never take down a listener's polling pass (e.g.
					// the exporter thread) -- skip a momentarily-failing owner this round
					// rather than propagating.
					continue;
				}

				// Tagged by the owner's identity hash so a reader with several concurrently
				// live towers/factories (or a test isolating its own instance's row) can tell
				// one instance's measurement apart from another's -- see the type-level remarks.
				yield return new Measurement<long>(value,
					new KeyValuePair<string, object?>("owner", RuntimeHelpers.GetHashCode(kv.Key)));
			}
		}
	}
}
