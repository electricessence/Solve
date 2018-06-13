using Open.Dataflow;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.Schemes
{
	public sealed partial class Classic<TGenome> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{
		public Classic(
			IGenomeFactory<TGenome> genomeFactory,
			ushort poolSize,
			ushort maxLevelLosses = 10,
			ushort maxLossesBeforeElimination = 30)
			: base(genomeFactory)
		{
			if (poolSize < 2)
				throw new ArgumentOutOfRangeException(nameof(poolSize), "Must be at least 2.");
			if (poolSize % 2 == 1)
				throw new ArgumentException("Must be a mutliple of 2.", nameof(poolSize));
			Contract.EndContractBlock();

			PoolSize = poolSize;
			MaxLevelLosses = maxLevelLosses;
			MaxLossesBeforeElimination = maxLossesBeforeElimination;
		}

		public readonly ushort PoolSize;
		public readonly ushort MaxLevelLosses;
		public readonly ushort MaxLossesBeforeElimination;

		readonly ConcurrentDictionary<IProblem<TGenome>, Tournament> ProblemHosts
			= new ConcurrentDictionary<IProblem<TGenome>, Tournament>();

		public override void AddProblems(IEnumerable<IProblem<TGenome>> problems)
		{
			foreach (var problem in problems)
			{
				var k = new Tournament(problem, this);
				k.Subscribe(e =>
				{
					var factory = Factory[0];
					//factory.EnqueueVariations(e.Genome);
					//factory.EnqueueForMutation(e.Genome);
					factory.EnqueueForBreeding(e.Genome, e.Fitness.SampleCount / 2);
					Broadcast((problem, e));
				});
				ProblemHosts.TryAdd(problem, k);
			}

			base.AddProblems(problems);
		}


		void Post(TGenome genome)
		{
			foreach (var host in ProblemHosts.Values)
				host.Post(genome);
		}

		protected override Task StartInternal(CancellationToken token)
			=> Task.Run(cancellationToken: token, action: () =>
			{
				Parallel.ForEach(Factory, new ParallelOptions
				{
					CancellationToken = token,
				}, Post);
			});
	}


}
