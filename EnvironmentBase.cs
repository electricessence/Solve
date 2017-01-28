/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/Genetic-Algorithm-Platform/blob/master/LICENSE.md
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Open.Collections;

namespace Solve
{

    // Defines the pipeline?
    public abstract class EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{

		protected readonly IGenomeFactory<TGenome> Factory;
		protected const ushort MIN_POOL_SIZE = 2;
		public readonly ushort PoolSize;

		readonly protected ConcurrentList<IProblem<TGenome>> Problems = new ConcurrentList<IProblem<TGenome>>();

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
				Problems.Add(problem);
		}

		protected abstract Task StartInternal();

		public Task Start(params IProblem<TGenome>[] problems)
		{
			AddProblems(problems);
			if (!Problems.HasAny())
				throw new InvalidOperationException("Cannot start without any registered 'Problems'");
			return StartInternal();
		}

		public abstract IObservable<KeyValuePair<IProblem<TGenome>, TGenome>> AsObservable();



	}


}
