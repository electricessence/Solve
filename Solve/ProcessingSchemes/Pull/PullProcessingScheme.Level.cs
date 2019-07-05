using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes.Pull
{
	// ReSharper disable once PossibleInfiniteInheritance
	[SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
	public sealed partial class PullProcessingScheme<TGenome>
	{
		sealed class Level : TowerLevelBase<TGenome, ProblemTower, PullProcessingScheme<TGenome>>
		{
			readonly LinkedListNode<Level> _levelNode;
			public Level NextLevel => _levelNode.Next?.Value;
			readonly Level PreviousLevel;

			public Level(
				int level,
				ProblemTower tower,
				LinkedListNode<Level> node)
				: base(level, tower?.Environment.PoolSize ?? throw new ArgumentNullException(nameof(tower)), tower)
			{
				_levelNode = node ?? throw new ArgumentNullException(nameof(node));
				Contract.EndContractBlock();

				PreviousLevel = node.Previous?.Value;
			}

			readonly double[][] BestLevelFitness;
			readonly double[][] BestProgressiveFitness;

			readonly ConcurrentQueue<(TGenome Genome, Fitness[] Fitness)> Winners
				= new ConcurrentQueue<(TGenome Genome, Fitness[] Fitness)>();

			readonly ConcurrentQueue<LevelEntry> Retained
				= new ConcurrentQueue<LevelEntry>();

			public async ValueTask<(TGenome Genome, Fitness[] Fitness)> GetNextChampionAsync(bool retain = false)
			{
				retry:

				if (!retain && Winners.TryDequeue(out var winner))
					return winner;

				// Step 1: get the next candidate from a lower level or the genome factory.
				var candidate = PreviousLevel == null
					? (Genome: Tower.Environment.Factory.Next(), Fitness: Tower.NewFitness())
					: await PreviousLevel.GetNextChampionAsync();

				// Step 2: get the results the problem as well as the level fitness and progressive fitness.
				var result = (await Tower.Problem.ProcessSampleAsync(candidate.Genome, Index)).Select((fitness, i) =>
				{
					var values = fitness.Results.Sum.ToArray();
					var progressiveFitness = candidate.Fitness[i];

					var level = UpdateFitnessesIfBetter(
						BestLevelFitness,
						values, i);

					var progressive = UpdateFitnessesIfBetter(
						BestProgressiveFitness,
						progressiveFitness
							.Merge(values)
							.Average
							.ToArray(), i);

					Debug.Assert(progressiveFitness.MetricAverages.All(ma => ma.Value <= ma.Metric.MaxValue));

					return (values, level, progressive);
				}).ToArray();

				// Step 3: if either the level fitness or progressive fitness is superior, pass on.
				if (result.Any(e => e.level.success || e.progressive.success))
				{
					if (retain) Winners.Enqueue(candidate);
					return candidate;
				}

				// Step 4: retain the inferior candidate.
				Retained.Enqueue(new LevelEntry(in candidate, result.Select(r => r.values).ToArray()));

				// Step 5: try again to get a winner
				goto retry;

			}

			protected override Task<LevelEntry> ProcessEntry((TGenome Genome, Fitness[] Fitness) champ)
			{
				throw new NotImplementedException();
			}
		}

	}
}
