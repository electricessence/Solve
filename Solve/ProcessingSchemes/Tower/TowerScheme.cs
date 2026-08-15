using Solve.Metrics;
using System.Diagnostics.Contracts;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Solve.ProcessingSchemes;

public partial class TowerScheme<TGenome> : TowerSchemeBase<TGenome>
	where TGenome : class, IGenome
{
	public TowerScheme(
		IGenomeFactory<TGenome> genomeFactory,
		SchemeConfig config,
		GenomeProgressionLog? genomeProgressionLog = null)
		: base(genomeFactory, config, genomeProgressionLog)
	{
	}

	public TowerScheme(
		IGenomeFactory<TGenome> genomeFactory,
		SchemeConfig.PoolSizing config,
		GenomeProgressionLog? genomeProgressionLog = null)
		: base(genomeFactory, new SchemeConfig { PoolSize = config }, genomeProgressionLog)
	{
	}

	private IEnumerable<ProblemTower>? ActiveTowers;

	// Aggregates every started tower's (per-problem) ProblemTower.LevelCreated into a
	// single scheme-wide stream, tagged with which problem the level belongs to. Exists
	// so hosts (interactive console runners, the headless benchmark harness) can report
	// level-creation themselves instead of the engine writing directly to the console --
	// see ProblemTower.OnLevelCreated, which no longer does so. Backed by a Subject
	// (rather than an Observable.Merge over ActiveTowers) because towers don't exist
	// until StartInternal runs, but a subscriber (e.g. RunnerBase.Start) needs a stable
	// observable to attach to before that -- StartInternal below feeds this subject as
	// each tower is created.
	private readonly Subject<(IProblem<TGenome> Problem, int Level)> _levelCreated = new();
	public IObservable<(IProblem<TGenome> Problem, int Level)> LevelCreated => _levelCreated.AsObservable();

	protected async ValueTask<int> PostAsync(TGenome genome)
	{
		ArgumentNullException.ThrowIfNull(genome);
		Contract.EndContractBlock();

		int count = 0;
		foreach (ProblemTower t in ActiveTowers!)
		{
			await t.PostAsync(genome).ConfigureAwait(false);
			++count;
		}

		return count;
	}

	protected override async Task StartInternal(CancellationToken token)
	{
		var towers = Problems.Select(p => new ProblemTower(Config, p, Factory, token)).ToList();
		// Subscribe onError as well: a tower fault (e.g. a failed level) must propagate
		// to scheme observers instead of dying as an unobserved exception.
		foreach (ProblemTower? tower in towers)
		{
			tower.Subscribe(Broadcast, Fault);
			tower.LevelCreated.Subscribe(level => _levelCreated.OnNext((tower.Problem, level)));
		}
		ActiveTowers = towers.Where(t => !t.Problem.HasConverged);

	retry:

		TGenome? genome = null;
		if (!token.IsCancellationRequested)
			genome = Factory.Next();

		if (genome is null) return;

		GenomeProgress?[genome.Hash].Add(GenomeEvent.EventType.Born);
		if (await PostAsync(genome).ConfigureAwait(false) == 0) return;

		goto retry;
	}

	protected override void OnDispose()
	{
		base.OnDispose();
		_levelCreated.Dispose();
	}
}
