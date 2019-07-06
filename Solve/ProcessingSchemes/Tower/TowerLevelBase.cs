using System.Diagnostics;

namespace Solve.ProcessingSchemes.Tower
{
	public abstract partial class TowerProcessingSchemeBase<TGenome, TTower, TEnvironment> : EnvironmentBase<TGenome>
	{
		public abstract class TowerLevelBase
		{
			public readonly int Index;
			protected readonly TTower Tower;

			protected TowerLevelBase(
				int level,
				TTower tower)
			{
				Debug.Assert(level >= 0);

				Index = level;
				Tower = tower;
			}
		}
	}
}
