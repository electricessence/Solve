/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Open.Arithmetic;
using Open.Collections;
using Open.Dataflow;
using System.Diagnostics;

namespace Solve.Schemes
{

	public sealed class PyramidPipeline<TGenome> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{
		readonly BroadcastBlock<KeyValuePair<IProblem<TGenome>, TGenome>> TopGenome = new BroadcastBlock<KeyValuePair<IProblem<TGenome>, TGenome>>(null);

		public override IObservable<KeyValuePair<IProblem<TGenome>, TGenome>> AsObservable()
		{
			return TopGenome.AsObservable();
		}

		readonly GenomeProducer<TGenome> Producer;

		readonly ITargetBlock<TGenome> FinalistPool;

		readonly GenomePipelineBuilder<TGenome> PipelineBuilder;

		readonly ISourceBlock<TGenome> Pipeline;

		readonly ActionBlock<TGenome> Breeders;

		readonly ActionBlock<TGenome> VipPool;

		const int ConvergenceThreshold = 20;

		public PyramidPipeline(
			IGenomeFactory<TGenome> genomeFactory,
			ushort poolSize,
			uint networkDepth = 3,
			byte nodeSize = 2,
			ushort finalistPoolSize = 0) : base(genomeFactory, poolSize)
		{
			if (poolSize < MIN_POOL_SIZE)
				throw new ArgumentOutOfRangeException("poolSize", poolSize, "Must have a pool size of at least " + MIN_POOL_SIZE);

			if (finalistPoolSize == 0)
				finalistPoolSize = poolSize;

			Producer = new GenomeProducer<TGenome>(Factory.Generate());

			Breeders = new ActionBlock<TGenome>(
				genome => Producer.TryEnqueue(Factory.Expand(genome)),
				new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 8 });

			Action<TGenome> addToBreeders = genome =>
			{
				//Debug.Assert(genome.Hash.Length != 0, "Attempting to breed an empty genome.");
				if (genome.Hash.Length != 0)
					Breeders.SendAsync(genome);
			};

			PipelineBuilder = new GenomePipelineBuilder<TGenome>(Producer, ProblemsInternal, poolSize, nodeSize,
				selected =>
				{
					var top = selected.FirstOrDefault();
					if (top != null) addToBreeders(top);
				},
				rejected =>
				{
					var rejects = rejected.Select(s => s.Hash);
					foreach (var p in ProblemsInternal)
					{
						// If they couldn't make it to the top of the pyramid, make sure they won't try again.
						p.Reject(rejects);
					}
				});

			Pipeline = PipelineBuilder.CreateNetwork(networkDepth); // 3? Start small?

			var TopGenomeFilter = TopGenome.OnlyIfChanged(DataflowMessageStatus.Accepted);
			bool converged = false;
			VipPool = new ActionBlock<TGenome>(
				async genome =>
				{
					var repost = false;
					long? batchID = null;
					foreach (var problem in ProblemsInternal)
					{
						var fitness = problem.GetFitnessFor(genome).Value.Fitness;
						if (fitness.HasConverged(0))
						{
							if (!fitness.HasConverged(ConvergenceThreshold)) // should be enough for perfect convergence.
							{
								if (!batchID.HasValue) batchID = SampleID.Next();
								// Give it some unseen data...
								problem.AddToGlobalFitness(
									new GenomeFitness<TGenome>(genome, await problem.TestProcessor(genome, batchID.Value)));
								repost = true;
							}
						}
					}
					if (repost)
						VipPool.Post(genome);

				},
				new ExecutionDataflowBlockOptions()
				{
					MaxDegreeOfParallelism = 3
				});

			FinalistPool = PipelineBuilder.Selector(
				finalistPoolSize,
				selection =>
				{
					var selected = selection.Selected;
					// Finalists use global fitness?
					// Get the top one for each problem.
					var topsConverged = ProblemsInternal.All(problem =>
					{
						var top = selected
							.OrderBy(g =>
								problem.GetFitnessFor(g, true).Value,
								GenomeFitness.Comparer<TGenome, Fitness>.Instance)
							.ThenByDescending(g => g.Hash.Length)
							.FirstOrDefault();

						if (top != null)
						{
							var gf = problem.GetFitnessFor(top).Value;
							var fitness = gf.Fitness;
							if (fitness.HasConverged(ConvergenceThreshold))
							{
								top = gf.Genome;
								Console.WriteLine("Converged: " + top);
								TopGenome.Post(KeyValuePair.Create(problem, top));

								//// Need at least 200 samples to wash out any double precision issues.
								//Problems.Process(
								//	new TGenome[] { top }.Concat((IReadOnlyList<TGenome>)top.Variations),

								//	Math.Max(fitness.SampleCount, 200))
								//	.ContinueWith(t =>
								//	{
								//		KeyValuePair<IProblem<TGenome>, GenomeFitness<TGenome>[]>[] r = t.Result;
								//		foreach (var p in r)
								//		{
								//			// foreach (var g in p.Value)
								//			// {
								//			// 	Console.WriteLine("{0}\n  \t[{1}] ({2} samples)", g.Genome, g.Fitness.Scores.JoinToString(","), g.Fitness.SampleCount);
								//			// }
								//			if (p.Value.Any())
								//			{
								//				var first = p.Value.First().Genome;
								//				if (first != top)
								//				{
								//					Console.WriteLine("Best Variation: " + first + " of " + top);
								//					TopGenomeFilter.Post(KeyValuePair.Create(problem, first));
								//				}

								//			}
								//		}
								//		TopGenome.Complete();
								//	});
								TopGenome.Complete();
								return true;
							}

							TopGenomeFilter.Post(KeyValuePair.Create(problem, top));

							// You made it all the way back to the top?  Forget about what I said...
							fitness.RejectionCount = -3; // VIPs get their rejection count augmented so they aren't easily dethroned.

							VipPool.SendAsync(top);

							//Producer.TryEnqueue((IReadOnlyList<TGenome>)top.Variations);

							// Top get's special treatment.
							for (var i = 0; i < networkDepth - 1; i++)
								addToBreeders(top);

							// Crossover.
							TGenome[] o2 = Factory.AttemptNewCrossover(top, Triangular.Disperse.Decreasing(selected).ToArray());
							if (o2 != null && o2.Length != 0)
								Producer.TryEnqueue(o2.Select(o => problem.GetFitnessFor(o)?.Genome ?? o)); // Get potential stored variation.
						}

						return false;
					});

					if (topsConverged)
					{
						converged = true;

						FinalistPool.Complete();
						VipPool.Complete();
						Breeders.Complete();
						Producer.Complete();
						// Calling Pipeline.Complete() will cause an abrupt exit.
					}
					else
					{
						// NOTE: That global GenomeFitness returns may return a 'version' of the actual genome.
						// Ensure the global pareto is retained. (note is using global version)
						var paretoGenomes = ProblemsInternal.SelectMany(p =>
								GenomeFitness.Pareto(p.GetFitnessFor(selection.All)).Select(g => g.Genome)
							)
							.Distinct()
							.ToArray();

						// Keep trying to breed pareto genomes since they conversely may have important genetic material.
						Producer.TryEnqueue(Factory.AttemptNewCrossover(paretoGenomes));

						var rejected = new HashSet<TGenome>(selection.Rejected);

						// Look for long term winners who have made it to the top before and don't discard them easily.
						var oldChampions
							= ProblemsInternal
								.SelectMany(p => p.GetFitnessFor(rejected, true))
								.Where(gf => gf.Fitness.IncrementRejection() < 1)
								.Select(gf => gf.Genome)
								.Distinct()
								.ToArray();

						rejected.ExceptWith(oldChampions);
						rejected.ExceptWith(paretoGenomes);

						// The top final pool recycles it's winners.
						foreach (var g in oldChampions.Concat(selected).Concat(paretoGenomes).Distinct()) //Also avoid re-entrance if there are more than one.
							FinalistPool.Post(g); // Might need to look at the whole pool and use pareto to retain.

						var recycled
							= ProblemsInternal
								.SelectMany(p => p.GetFitnessFor(rejected, true))
								.Where(gf => gf.Fitness.RejectionCount < 5)
								.Select(gf => gf.Genome)
								.Distinct()
								.ToArray();
						
						// Just in case a challenger got lucky.
						Producer.TryEnqueue(recycled, true);

						rejected.ExceptWith(recycled);

						// True rejects remain and get marked. :(
						var rejects = rejected.Select(r => r.Hash).ToArray();
						foreach (var p in ProblemsInternal)
							p.Reject(rejects);

					}
				})
				.PropagateFaultsTo(TopGenome)
				.PropagateCompletionTo(TopGenome, VipPool, Breeders, Pipeline);


			Pipeline.LinkToWithExceptions(FinalistPool);
			Pipeline
				.OnFault(ex => Console.WriteLine(ex))
				.OnComplete(() => Console.WriteLine("Pipeline COMPLETED"));
			Pipeline.PropagateCompletionTo(Producer);



			TopGenome.PropagateCompletionTo(Pipeline);
			VipPool.PropagateFaultsTo(Pipeline);
			Producer.PropagateFaultsTo(Pipeline);
			Breeders.PropagateFaultsTo(Pipeline);

			Producer
				.ProductionCompetion
				.ContinueWith(task =>
				{
					if (!converged)
						Pipeline.Fault("Producer Completed Unexpectedly.");
				});

		}

		public void AddSeed(TGenome genome)
		{
			FinalistPool.Post(genome);
		}

		public void AddSeeds(IEnumerable<TGenome> genomes)
		{
			foreach(var g in genomes)
				FinalistPool.Post(g);
		}

		protected override Task StartInternal()
		{
			var completed = TopGenome.Completion;
			Producer.Poke();
			return completed;
		}
	}


}
