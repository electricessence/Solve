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
			in (ushort First, ushort Minimum, ushort Step) poolSize,
			ushort maxLevels = ushort.MaxValue,
			ushort maxLevelLosses = DEFAULT_MAX_LEVEL_LOSSES,
			ushort maxLossesBeforeElimination = DEFAULT_MAX_LOSSES_BEFORE_ELIMINATION,
			GenomeProgressionLog? genomeProgressionLog = null)
			: base(metrics, genomeFactory, in poolSize, maxLevels, maxLevelLosses, maxLossesBeforeElimination)
		{

		}

		public TowerProcessingScheme(
			IMetricsRoot metrics,
			IGenomeFactory<TGenome> genomeFactory,
			ushort poolSize,
			ushort maxLevels = ushort.MaxValue,
			ushort maxLevelLosses = DEFAULT_MAX_LEVEL_LOSSES,
			ushort maxLossesBeforeElimination = DEFAULT_MAX_LOSSES_BEFORE_ELIMINATION)
			: this(metrics, genomeFactory, (poolSize, poolSize, 2), maxLevels, maxLevelLosses, maxLossesBeforeElimination) { }


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
