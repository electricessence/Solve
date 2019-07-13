using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes.Tower
{
#if DEBUG
	using System.Diagnostics;
#endif

	// ReSharper disable once PossibleInfiniteInheritance
	public sealed partial class TowerProcessingScheme<TGenome> : TowerProcessingSchemeBase<TGenome>
		where TGenome : class, IGenome
	{
		public TowerProcessingScheme(
			IGenomeFactory<TGenome> genomeFactory,
			in (ushort First, ushort Minimum, ushort Step) poolSize,
			ushort maxLevels = ushort.MaxValue,
			ushort maxLevelLosses = DEFAULT_MAX_LEVEL_LOSSES,
			ushort maxLossesBeforeElimination = DEFAULT_MAX_LOSSES_BEFORE_ELIMINATION)
			: base(genomeFactory, in poolSize, maxLevels, maxLevelLosses, maxLossesBeforeElimination)
		{

		}

		public TowerProcessingScheme(
			IGenomeFactory<TGenome> genomeFactory,
			ushort poolSize,
			ushort maxLevels = ushort.MaxValue,
			ushort maxLevelLosses = DEFAULT_MAX_LEVEL_LOSSES,
			ushort maxLossesBeforeElimination = DEFAULT_MAX_LOSSES_BEFORE_ELIMINATION)
			: this(genomeFactory, (poolSize, poolSize, 2), maxLevels, maxLevelLosses, maxLossesBeforeElimination) { }


		//IReadOnlyList<ProblemTower> Towers;
		private IEnumerable<ProblemTower> ActiveTowers;

		protected override Task StartInternal(CancellationToken token)
		{
			var towers = Problems.Select(p => new ProblemTower(p, this)).ToList();
			//Towers = towers.AsReadOnly();
			ActiveTowers = towers.Where(t => !t.Problem.HasConverged);
			return base.StartInternal(token);
		}

		protected override void Post(TGenome genome)
		{
			if (genome == null) throw new ArgumentNullException(nameof(genome));
			Contract.EndContractBlock();

#if DEBUG
			Debug.WriteLineIf(EMIT_GENOMES,
				$"Posting:\n{GetGenomeInfo(genome)}\n",
				"TowerProcessingScheme");
#endif

			foreach (var t in ActiveTowers)
				t.Post(genome);

			foreach (var t in ActiveTowers)
				t.ProcessPools();
		}

	}


}
