using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes.Pull
{
	// ReSharper disable once PossibleInfiniteInheritance
	public sealed partial class PullProcessingScheme<TGenome> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{
		const ushort DEFAULT_MAX_LEVEL_LOSSES = 3;
		const ushort DEFAULT_MAX_LOSSES_BEFORE_ELIMINATION = DEFAULT_MAX_LEVEL_LOSSES * 30;

		internal readonly (ushort First, ushort Minimum, ushort Step) PoolSize;

		public PullProcessingScheme(
			IGenomeFactory<TGenome> genomeFactory,
			in (ushort First, ushort Minimum, ushort Step) poolSize,
			ushort maxLevels = ushort.MaxValue,
			ushort maxLevelLosses = DEFAULT_MAX_LEVEL_LOSSES,
			ushort maxLossesBeforeElimination = DEFAULT_MAX_LOSSES_BEFORE_ELIMINATION)
			: base(genomeFactory)
		{
			PoolSize = poolSize;
		}

		public PullProcessingScheme(
			IGenomeFactory<TGenome> genomeFactory,
			ushort poolSize,
			ushort maxLevels = ushort.MaxValue,
			ushort maxLevelLosses = DEFAULT_MAX_LEVEL_LOSSES,
			ushort maxLossesBeforeElimination = DEFAULT_MAX_LOSSES_BEFORE_ELIMINATION)
			: this(genomeFactory, (poolSize, poolSize, 2), maxLevels, maxLevelLosses, maxLossesBeforeElimination) { }


		//IReadOnlyList<ProblemTower> Towers;
		//private IEnumerable<ProblemTower> ActiveTowers;

		protected override Task StartInternal(CancellationToken token)
		{
			var towers = Problems.Select(p => new ProblemTower(p, this)).ToList();
			//Towers = towers.AsReadOnly();
			//ActiveTowers = towers.Where(t => !t.Problem.HasConverged);

			return Task.Run(() =>
			{

			}, token);
		}

	}


}
