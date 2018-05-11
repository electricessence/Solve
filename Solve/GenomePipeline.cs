using Open.Collections;
using Open.Dataflow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using KVP = Open.Collections.KeyValuePair;
namespace Solve
{
	// GenomeSelection should be short lived.
	public struct GenomeSelection<TGenome>
		where TGenome : IGenome
	{
		public readonly TGenome[] All;
		public readonly TGenome[] Selected;
		public readonly TGenome[] Rejected;

		public GenomeSelection(IEnumerable<TGenome> results) : this(results.ToArray())
		{

		}
		public GenomeSelection(TGenome[] results)
		{
			int len = results.Length;
			int selectionPoint = len / 2;
			var all = new TGenome[len];
			var selected = new TGenome[selectionPoint];
			var rejected = new TGenome[len - selectionPoint];
			for (var i = 0; i < len; i++)
			{
				var g = results[i];
				all[i] = g;
				if (i < selectionPoint)
					selected[i] = g;
				else
					rejected[i - selectionPoint] = g;
			}

			All = all;
			Selected = selected;
			Rejected = rejected;
		}

	}

	public static class GenomePipeline
	{
		static readonly ExecutionDataflowBlockOptions Max2Queued = new ExecutionDataflowBlockOptions()
		{
			BoundedCapacity = 2
		};

		public static TransformBlock<
			IDictionary<IProblem<TGenome>, Task<IGenomeFitness<TGenome>>[]>,
			Dictionary<IProblem<TGenome>, IGenomeFitness<TGenome>[]>> Processor<TGenome>()
			where TGenome : IGenome
		{
			return new TransformBlock<
			IDictionary<IProblem<TGenome>, Task<IGenomeFitness<TGenome>>[]>,
			Dictionary<IProblem<TGenome>, IGenomeFitness<TGenome>[]>>(
				async problems =>
					(await Task.WhenAll(problems.Select(
						async kvp =>
						KVP.Create(
							kvp.Key,
							await Task.WhenAll(kvp.Value).ContinueWith(t =>
						{
							var a = t.Result;
							Array.Sort(a, GenomeFitness.Comparison);
							return a;
						}))))
					).ToDictionary(),
				new ExecutionDataflowBlockOptions
				{
					MaxDegreeOfParallelism = 64
				});
		}

		// public static IPropagatorBlock<TGenome, GenomeFitness<TGenome>[]>
		// 	ProcessorBatched<TGenome>(
		// 		GenomeTestDelegate<TGenome> test,
		// 		int size)
		// 	where TGenome : IGenome
		// {
		// 	var input = new BatchBlock<TGenome>(size, new GroupingDataflowBlockOptions
		// 	{
		// 		BoundedCapacity = size * 2
		// 	});

		// 	var output = new TransformBlock<TGenome[], GenomeFitness<TGenome>[]>(async batch =>
		// 	{
		// 		long batchId = UniqueBatchID();
		// 		return await Task.WhenAll(batch.Select(g => test(g, batchId).ContinueWith(t => new GenomeFitness<TGenome>(g, t.Result))))
		// 			.ContinueWith(task => task.Result.OrderBy(g => g, GenomeFitness.Comparer<TGenome>.Instance)
		// 				.ToArray());
		// 	}, new ExecutionDataflowBlockOptions
		// 	{
		// 		MaxDegreeOfParallelism = 32
		// 	});

		// 	input.LinkToWithExceptions(output);

		// 	return DataflowBlock.Encapsulate(input, output);
		// }

		public static IPropagatorBlock<TGenome, Dictionary<IProblem<TGenome>, IGenomeFitness<TGenome>[]>>
			Processor<TGenome>(
				IEnumerable<IProblem<TGenome>> problems,
				int size)
			where TGenome : IGenome
		{
			if (size < 1)
				throw new ArgumentOutOfRangeException(nameof(size), size, "Must be at least 1.");

			var output = Processor<TGenome>();

			var sync = new Object();
			int index = -2;
			long batchId = -1;
			Dictionary<IProblem<TGenome>, Task<IGenomeFitness<TGenome>>[]> results = null;

			var input = new ActionBlock<TGenome>(genome =>
			{
				if (!genome.IsReadOnly)
					throw new InvalidOperationException("Cannot process an unfrozen genome.");
				//if (genome.Hash.Length == 0)
				//	throw new InvalidOperationException("Cannot process a genome with an empty hash.");
				if (index == -2)
				{
					batchId = SampleID.Next();
					results = problems.ToDictionary(e => e, e => new Task<IGenomeFitness<TGenome>>[size]);
					if (results.Count == 0)
						throw new Exception("No problems provided to process.");
					index = -1;
				}

				index++;
				if (index < size)
				{
					foreach (var kvp in results)
					{
						kvp.Value[index] = kvp.Key.TestProcessor(genome, batchId)
							.ContinueWith(t => (IGenomeFitness<TGenome>)(new GenomeFitness<TGenome>(genome, t.Result)));
					}
				}
				else
				{
					output.SendAsync(results);
					index = -2;
				}

			}, new ExecutionDataflowBlockOptions
			{
				BoundedCapacity = size
			});

			input.PropagateFaultsTo(output);

			return DataflowBlock.Encapsulate(input, output);
		}

		public static TransformBlock<
			IDictionary<IProblem<TGenome>, IGenomeFitness<TGenome>[]>,
			GenomeSelection<TGenome>>
		Selector<TGenome>()
			where TGenome : IGenome
		{
			return new TransformBlock<IDictionary<IProblem<TGenome>, IGenomeFitness<TGenome>[]>, GenomeSelection<TGenome>>(results =>
			{
				foreach (var kvp in results)
					kvp.Key.AddToGlobalFitness(kvp.Value);
				return new GenomeSelection<TGenome>(results.Select(kvp => kvp.Value).Weave().Select(r => r.Genome).Distinct());
			}, new ExecutionDataflowBlockOptions()
			{
				MaxDegreeOfParallelism = 2,
				BoundedCapacity = 2
			});
		}

		public static IPropagatorBlock<TGenome, GenomeSelection<TGenome>>
			Selector<TGenome>(IEnumerable<IProblem<TGenome>> problems, int size, int count = 1)
			where TGenome : IGenome
		{
			if (problems == null)
				throw new ArgumentNullException(nameof(problems));
			if (size < 2)
				throw new ArgumentOutOfRangeException(nameof(size), size, "Must be at least 2.");
			if (count < 1)
				throw new ArgumentOutOfRangeException(nameof(count), size, "Must be at least 1.");

			var processor = Processor(problems, size);
			var selector = Selector<TGenome>();
			processor.LinkToWithExceptions(selector);

			return DataflowBlock.Encapsulate(processor, selector);
		}

		public static ITargetBlock<TGenome>
			Distributor<TGenome>(
				IEnumerable<IProblem<TGenome>> problems,
				int size,
				ITargetBlock<TGenome[]> selected,
				ITargetBlock<TGenome[]> rejected = null)
			where TGenome : IGenome
		{
			if (selected == null)
				throw new ArgumentNullException(nameof(selected));

			var input = Selector(problems, size)
				.PropagateFaultsTo(selected, rejected);

			input.LinkTo(new ActionBlock<GenomeSelection<TGenome>>(async selection =>
			{
				await selected.SendAsync(selection.Selected);
				if (rejected != null)
					await rejected.SendAsync(selection.Rejected).ConfigureAwait(false);
			}, Max2Queued));

			return input;
		}

		public static ITargetBlock<TGenome>
			Distributor<TGenome>(
				IEnumerable<IProblem<TGenome>> problems,
				int size,
				Action<GenomeSelection<TGenome>> selection)
			where TGenome : IGenome
		{
			if (selection == null)
				throw new ArgumentNullException(nameof(selection));

			var input = Selector(problems, size);
			input.LinkTo(selection);
			return input;
		}

		public static ITargetBlock<TGenome>
			Distributor<TGenome>(
				IEnumerable<IProblem<TGenome>> problems,
				int size,
				Action<TGenome[]> selected,
				Action<TGenome[]> rejected = null)
			where TGenome : IGenome
		{
			return Distributor(problems, size,
				new ActionBlock<TGenome[]>(selected, Max2Queued),
				rejected == null ? null : new ActionBlock<TGenome[]>(rejected, Max2Queued));
		}

		public static ISourceBlock<TGenome> Node<TGenome>(
			IEnumerable<ISourceBlock<TGenome>> sources,
			IEnumerable<IProblem<TGenome>> problems,
			int size,
			Action<TGenome[]> globalSelectedHandler = null,
			Action<TGenome[]> globalRejectedHandler = null)
			where TGenome : IGenome
		{
			var output = new BufferBlock<TGenome>();
			var processor = Distributor(problems, size,
				selected =>
				{
					foreach (var g in selected)
						output.SendAsync(g);

					globalSelectedHandler?.Invoke(selected);
				},
				globalRejectedHandler)
				.PropagateFaultsTo(output);

			foreach (var source in sources)
				source.LinkToWithExceptions(processor);

			return output;
		}

		public static ISourceBlock<TGenome> Node<TGenome>(
			ISourceBlock<TGenome> source,
			IEnumerable<IProblem<TGenome>> problems,
			int size,
			Action<TGenome[]> globalHandler = null)
			where TGenome : IGenome
		{
			return Node(new ISourceBlock<TGenome>[] { source }, problems, size, globalHandler);
		}


	}

	public class GenomePipelineBuilder<TGenome>
		where TGenome : IGenome
	{
		public readonly ushort PoolSize;
		public readonly byte SourceCount;

		readonly ISourceBlock<TGenome> DefaultSource;

		readonly ICollection<IProblem<TGenome>> Problems;

		readonly Action<TGenome[]> GlobalSelectedHandler;
		readonly Action<TGenome[]> GlobalRejectedHandler;

		public GenomePipelineBuilder(
			ISourceBlock<TGenome> defaultSource,
			ICollection<IProblem<TGenome>> problems,
			ushort poolSize,
			byte sourceCount = 2,
			Action<TGenome[]> globalSelectedHandler = null,
			Action<TGenome[]> globalRejectedHandler = null)
		{
			DefaultSource = defaultSource ?? throw new ArgumentNullException(nameof(defaultSource));
			Problems = problems ?? throw new ArgumentNullException(nameof(problems));
			PoolSize = poolSize;
			if (sourceCount < 1)
				throw new ArgumentOutOfRangeException(nameof(sourceCount), sourceCount, "Must be at least 1.");
			SourceCount = sourceCount;
			GlobalSelectedHandler = globalSelectedHandler;
			GlobalRejectedHandler = globalRejectedHandler;
		}

		public void AddProblem(IProblem<TGenome> problem)
		{
			if (problem == null)
				throw new ArgumentNullException(nameof(problem));
			lock (Problems)
			{
				if (Problems.Contains(problem))
					throw new ArgumentException("Already registered", nameof(problem));

				Problems.Add(problem);
			}

		}

		public ISourceBlock<TGenome> Node(
			ISourceBlock<TGenome> source = null)
		{
			return GenomePipeline.Node(new ISourceBlock<TGenome>[] { source ?? DefaultSource }, Problems, PoolSize, GlobalSelectedHandler);
		}

		IEnumerable<ISourceBlock<TGenome>> FirstLevelNodes()
		{
			for (byte i = 0; i < SourceCount; i++)
				yield return Node();
		}

		public ISourceBlock<TGenome> CreateNextLevelNode(IEnumerable<ISourceBlock<TGenome>> sources = null)
		{
			return GenomePipeline.Node(sources ?? FirstLevelNodes(), Problems, PoolSize, GlobalSelectedHandler);
		}

		// source node count = depth^SourceCount
		public ISourceBlock<TGenome> CreateNetwork(uint depth = 1)
		{
			if (depth == 0)
				throw new ArgumentOutOfRangeException(nameof(depth), depth, "Must be at least 1.");
			if (depth == 1) return Node();
			if (depth == 2) return CreateNextLevelNode();

			return CreateNextLevelNode(
				Enumerable.Range(0, SourceCount)
					.Select(i => CreateNetwork(depth - 1))
			);
		}

		public ITargetBlock<TGenome>
			Distributor(
				Action<TGenome[]> selected,
				Action<TGenome[]> rejected = null)
		{
			return GenomePipeline.Distributor(Problems, PoolSize,
				new ActionBlock<TGenome[]>(selected),
				rejected == null ? null : new ActionBlock<TGenome[]>(rejected));
		}

		public ITargetBlock<TGenome>
			Selector(Action<GenomeSelection<TGenome>> selection)
		{
			return GenomePipeline.Distributor(Problems, PoolSize, selection);
		}

		public ITargetBlock<TGenome>
			Selector(int poolSize, Action<GenomeSelection<TGenome>> selection)
		{
			return GenomePipeline.Distributor(Problems, poolSize, selection);
		}
	}

	public static class GenomePipelineBuilder
	{
		public static GenomePipelineBuilder<TGenome> New<TGenome>(
			ISourceBlock<TGenome> defaultSource,
			IList<IProblem<TGenome>> problems,
			ushort poolSize,
			byte sourceCount = 2,
			Action<TGenome[]> globalSelectedHandler = null)
			where TGenome : IGenome
		{
			return new GenomePipelineBuilder<TGenome>(defaultSource, problems, poolSize, sourceCount, globalSelectedHandler);
		}

	}

}
