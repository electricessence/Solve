﻿/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using Open.Collections;
using Open.Collections.Synchronized;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Solve
{
	// Defines the pipeline?
	public abstract class EnvironmentBase<TGenome> : BroadcasterBase<(IProblem<TGenome> Problem, IGenomeFitness<TGenome> GenomeFitness)>
		where TGenome : class, IGenome
	{
		protected readonly IGenomeFactory<TGenome> Factory;

		readonly protected ISynchronizedCollection<IProblem<TGenome>> ProblemsInternal = new ReadWriteSynchronizedList<IProblem<TGenome>>();

		public ICollection<IProblem<TGenome>> Problems
		{
			get
			{
				return ProblemsInternal.Snapshot();
			}
		}

		protected EnvironmentBase(IGenomeFactory<TGenome> genomeFactory)
			: base()
		{
			Factory = genomeFactory;
		}

		public void AddProblem(params IProblem<TGenome>[] problems)
		{
			AddProblems(problems);
		}

		public virtual void AddProblems(IEnumerable<IProblem<TGenome>> problems)
		{
			foreach (var problem in problems)
				ProblemsInternal.Add(problem);
		}

		protected readonly CancellationTokenSource Canceller = new CancellationTokenSource();

		int _state = 0;

		public Task Start(params IProblem<TGenome>[] problems)
		{
			switch (Interlocked.CompareExchange(ref _state, 1, 0))
			{
				case -1:
					throw new InvalidOperationException("Cannot start if cancellation requested.");

				case 0:
					if (Canceller.IsCancellationRequested)
						goto case -1;

					AddProblems(problems);
					if (!ProblemsInternal.HasAny())
						throw new InvalidOperationException("Cannot start without any registered 'Problems'");

					return StartInternal(Canceller.Token);

				case 1:
					throw new InvalidOperationException("Already started.");

			}

			return null;
		}

		protected abstract Task StartInternal(CancellationToken token);

		protected abstract void OnCancelled();

		public void Cancel()
		{
			if (-1 != Interlocked.Exchange(ref _state, -1))
			{
				Canceller.Cancel();
				OnCancelled();
			};
		}
	}


}
