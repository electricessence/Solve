using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes.Tower
{
	using App.Metrics;
	using Solve.Metrics;
#if DEBUG
	using System.Diagnostics;
#endif

	// ReSharper disable once PossibleInfiniteInheritance
	public sealed partial class TowerProcessingScheme<TGenome> : TowerProcessingSchemeBase<TGenome>
		where TGenome : class, IGenome
	{
		public TowerProcessingScheme(
			IMetricsRoot metrics,
			IGenomeFactory<TGenome> genomeFactory,
			SchemeConfig config,
			GenomeProgressionLog? genomeProgressionLog = null)
			: base(metrics, genomeFactory, config, genomeProgressionLog)
		{

		}

		public TowerProcessingScheme(
			IMetricsRoot metrics,
			IGenomeFactory<TGenome> genomeFactory,
			SchemeConfig.PoolSizing config,
			GenomeProgressionLog? genomeProgressionLog = null)
			: this(metrics, genomeFactory, new SchemeConfig { PoolSize = config }, genomeProgressionLog)
		{

		}

		//IReadOnlyList<ProblemTower> Towers;
		private IEnumerable<ProblemTower>? ActiveTowers;

		protected override Task StartInternal(CancellationToken token)
		{
			var towers = Problems.Select(p => new ProblemTower(p, this)).ToList();
			//Towers = towers.AsReadOnly();
			ActiveTowers = towers.Where(t => !t.Problem.HasConverged);
			return base.StartInternal(token);
		}

		protected override async ValueTask PostAsync(TGenome genome)
		{
			if (genome is null) throw new ArgumentNullException(nameof(genome));
			Contract.EndContractBlock();

#if DEBUG
			Debug.WriteLineIf(EMIT_GENOMES,
				$"Posting:\n{GetGenomeInfo(genome)}\n",
				"TowerProcessingScheme");
#endif

			foreach (var t in ActiveTowers!)
				await t.PostAsync(genome).ConfigureAwait(false);

			foreach (var t in ActiveTowers)
				await t.ProcessPoolsAsync().ConfigureAwait(false);
		}

	}


}
