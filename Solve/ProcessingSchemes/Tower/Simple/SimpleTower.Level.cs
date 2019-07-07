using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes.Tower.Simple
{
	public partial class SimpleTowerProcessingScheme<TGenome> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{
		class Level : TowerLevelBase<TGenome, Tower, SimpleTowerProcessingScheme<TGenome>>
		{
			internal Level(int level, Tower tower) : base(level, tower)
			{
				BestProgressiveFitness = new double[tower.Problem.Pools.Count][];
			}

			readonly double[][] BestProgressiveFitness;

			Level _nextLevel;
			private Level NextLevel => LazyInitializer.EnsureInitialized(ref _nextLevel,
				() => new Level(Index + 1, Tower));

			readonly ConcurrentQueue<(TGenome Genome, Fitness[] Fitness)> Challengers
				= new ConcurrentQueue<(TGenome Genome, Fitness[] Fitness)>();

			public ValueTask ProcessChallenger()
				=> Challengers.TryDequeue(out var challenger)
				? ProcessChallengerCore(challenger)
				: new ValueTask();


			async ValueTask ProcessChallengerCore((TGenome Genome, Fitness[] Fitness) challenger)
			{
				var result = (await Tower.Problem.ProcessSampleAsync(challenger.Genome, Index)).Select((fitness, i) =>
				{
					var values = fitness.Results.Sum.ToArray();
					var progressiveFitness = challenger.Fitness[i];
					var (success, fresh) = UpdateFitnessesIfBetter(
						BestProgressiveFitness,
						progressiveFitness
							.Merge(values)
							.Average
							.ToArray(), i);

					Debug.Assert(progressiveFitness.MetricAverages.All(ma => ma.Value <= ma.Metric.MaxValue));

					return (values, success, fresh);
				}).ToArray();

				if (IsTop)
				{
					for (byte i = 0; i < result.Length; i++)
					{
						if (result[i].success)
							ProcessChampion(i, challenger);
					}
				}

				if (result.Any(r => r.fresh) || !result.Any(r => r.success))
					return LevelEntry<TGenome>.Init(in challenger, result.Select(r => r.values).ToArray());

				Factory.EnqueueChampion(challenger.Genome);
				PostNextLevel(0, challenger);
				return null;
			}

			public void Post((TGenome Genome, Fitness[] Fitness) challenger)
			{
				Challengers.Enqueue(challenger);
				Tower.LevelsInNeed.Enqueue(this);
			}
		}

	}
}
