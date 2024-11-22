using Solve.Metrics;
using System.Diagnostics.Contracts;

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
		var towers = Problems.Select(p => new ProblemTower(Config, p, Factory)).ToList();
		foreach (ProblemTower? tower in towers) tower.Subscribe(Broadcast);
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
}
