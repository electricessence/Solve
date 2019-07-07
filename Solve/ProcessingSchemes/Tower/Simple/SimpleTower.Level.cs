using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes.Tower.Simple
{
	public partial class SimpleTowerProcessingScheme<TGenome> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{
		class Level : PostingTowerLevelBase<TGenome, Tower, SimpleTowerProcessingScheme<TGenome>>
		{
			internal Level(int level, Tower tower) : base(level, tower, 2)
			{
			}

			Level _nextLevel;
			protected override PostingTowerLevelBase<TGenome, Tower, SimpleTowerProcessingScheme<TGenome>> NextLevel
				=> LazyInitializer.EnsureInitialized(ref _nextLevel, () => new Level(Index + 1, Tower));

			protected override bool IsTop => _nextLevel == null;

			public override void Post(byte priority, (TGenome Genome, Fitness[] Fitness) challenger)
			{
				base.Post(priority, challenger);
				Tower.LevelsInNeed.Enqueue(this);
			}

			protected override ValueTask ProcessRetained(ConcurrentQueue<LevelEntry<TGenome>> retained)
			{
				throw new System.NotImplementedException();
			}
		}

	}
}
