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
				=> LevelsInNeed.TryDequeue(out var level)
				? ProcessCore(level)
				: new ValueTask();

			protected async ValueTask ProcessCore(Level level)
			{
				if(LevelsInNeed.TryDequeue(out var level))
				{
					//level.

				while (await level.ProcessChallenger()) { }
				await level.ProcessRetained();
			}

			public readonly ConcurrentQueue<Level> LevelsInNeed = new ConcurrentQueue<Level>();
		}
	}
}
