﻿using Open.Dataflow;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	public abstract class ProcessingSchemeBase<TGenome, TTower> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
		where TTower : GenomeFitnessBroadcasterBase<TGenome>, IGenomeProcessor<TGenome>
	{
		protected ProcessingSchemeBase(IGenomeFactory<TGenome> genomeFactory)
			: base(genomeFactory)
		{

		}

		protected readonly ConcurrentDictionary<IProblem<TGenome>, TTower> Towers
			= new ConcurrentDictionary<IProblem<TGenome>, TTower>();

		protected abstract TTower NewTower(IProblem<TGenome> problem);

		protected virtual bool OnTowerBroadcast(TTower source, IGenomeFitness<TGenome, Fitness> genomeFitness)
		{
			Factory[0].EnqueueChampion(genomeFitness.Genome);
			return true;
		}

		public override void AddProblems(IEnumerable<IProblem<TGenome>> problems)
		{
			foreach (var problem in problems)
			{
				var k = NewTower(problem);
				k.Subscribe(e =>
				{
					if (OnTowerBroadcast(k, e))
						Broadcast((problem, e));
				});
				Towers.TryAdd(problem, k);
			}

			base.AddProblems(problems);
		}

		void Post(TGenome genome)
		{
			foreach (var host in Towers.Values)
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