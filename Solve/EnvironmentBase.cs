/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using Open.Collections;
using Open.Collections.Synchronized;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Solve
{

	// Defines the pipeline?
	public abstract class EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{

		protected readonly IGenomeFactory<TGenome> Factory;
		protected const ushort MIN_POOL_SIZE = 2;
		public readonly ushort PoolSize;

		readonly protected LockSynchronizedList<IProblem<TGenome>> ProblemsInternal = new LockSynchronizedList<IProblem<TGenome>>();

		public IProblem<TGenome>[] Problems
		{
			get
			{
				return ProblemsInternal.Snapshot();
			}
		}

		protected EnvironmentBase(
			IGenomeFactory<TGenome> genomeFactory,
			ushort poolSize)
		{
			Factory = genomeFactory;
			PoolSize = poolSize;
		}

		public void AddProblem(params IProblem<TGenome>[] problems)
		{
			AddProblems(problems);
		}

		public void AddProblems(IEnumerable<IProblem<TGenome>> problems)
		{
			foreach (var problem in problems)
				ProblemsInternal.Add(problem);
		}

		protected abstract Task StartInternal();

		public Task Start(params IProblem<TGenome>[] problems)
		{
			AddProblems(problems);
			if (!ProblemsInternal.HasAny())
				throw new InvalidOperationException("Cannot start without any registered 'Problems'");
			return StartInternal();
		}

		public abstract IObservable<KeyValuePair<IProblem<TGenome>, TGenome>> AsObservable();



	}


}
