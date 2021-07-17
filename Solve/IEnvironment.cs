using System;
using System.Collections.Generic;
using System.Linq;

namespace Solve
{
	public interface IEnvironment<TGenome>
		: IObservable<(TGenome Genome, Fitness, IProblem<TGenome> Problem, int PoolIndex)>
		where TGenome : class, IGenome
	{
		public IReadOnlyList<IProblem<TGenome>> Problems { get; }

		void AddProblem(IProblem<TGenome> problem);

		public void AddProblems(IEnumerable<IProblem<TGenome>> problems)
		{
			if (problems == null)
				throw new ArgumentNullException(nameof(problems));

			foreach (var problem in problems)
				AddProblem(problem);
		}

		public void AddProblems(
			IProblem<TGenome> problem1,
			IProblem<TGenome> problem2,
			params IProblem<TGenome>[] problems)
		{
			AddProblem(problem1);
			AddProblem(problem2);
			AddProblems(problems);
		}

		bool HaveAllProblemsConverged { get; }
	}


}
