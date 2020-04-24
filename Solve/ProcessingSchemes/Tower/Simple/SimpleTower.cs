using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes.Tower.Simple
{
	public partial class SimpleTowerProcessingScheme<TGenome> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{
		class Tower : TowerBase<TGenome, SimpleTowerProcessingScheme<TGenome>>
		{
			public Tower(IProblem<TGenome> problem, SimpleTowerProcessingScheme<TGenome> environment) : base(problem, environment)
			{
			}

			public ValueTask Process()
			{
				if(LevelsInNeed.TryDequeue(out var level))
				{
					//level.
				}
			}

			public readonly ConcurrentQueue<Level> LevelsInNeed = new ConcurrentQueue<Level>();
		}
	}
}
