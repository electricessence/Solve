using System.Diagnostics;

namespace Solve.ProcessingSchemes.Tower
{
	public abstract class TowerLevelBase<TGenome, TTower, TEnvironment>
		where TGenome : class, IGenome
		where TTower : TowerBase<TGenome, TEnvironment>
		where TEnvironment : EnvironmentBase<TGenome>
	{
		protected TowerLevelBase(
			int level,
			TTower tower)
		{
			Debug.Assert(level >= 0);

			Index = level;
			Tower = tower;
		}

		public readonly int Index;
		protected readonly TTower Tower;
	}
}
