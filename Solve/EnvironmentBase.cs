/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using Open.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Solve
{
	// Defines the pipeline?
	// ReSharper disable once PossibleInfiniteInheritance
	public abstract class EnvironmentBase<TGenome>
		: BroadcasterBase<(IProblem<TGenome> Problem, (TGenome Genome, int PoolIndex, Fitness Fitness) Update)>
		where TGenome : class, IGenome
	{
		protected internal readonly IGenomeFactory<TGenome> Factory;

		protected readonly List<IProblem<TGenome>> ProblemsInternal;
		public readonly IReadOnlyList<IProblem<TGenome>> Problems;

		protected EnvironmentBase(IGenomeFactory<TGenome> genomeFactory)
		{
			Factory = genomeFactory ?? throw new ArgumentNullException(nameof(genomeFactory));
			Contract.EndContractBlock();

			ProblemsInternal = new List<IProblem<TGenome>>();
			Problems = ProblemsInternal.AsReadOnly();
		}

		public void AddProblem(params IProblem<TGenome>[] problems)
		{
			AddProblems(problems);
		}

		public void AddProblems(IEnumerable<IProblem<TGenome>> problems)
		{
			if (problems == null)
				throw new ArgumentNullException(nameof(problems));
			Contract.EndContractBlock();

			if (_state != 0)
				throw new InvalidOperationException("Attempting to add a problem when the environment has already started.");

			foreach (var problem in problems)
				ProblemsInternal.Add(problem);
		}

		public bool HaveAllProblemsConverged => ProblemsInternal.TrueForAll(p => p.HasConverged);

		protected readonly CancellationTokenSource Canceller = new CancellationTokenSource();

		public CancellationToken GetToken() => Canceller.Token;

		int _state;

		public Task Start(params IProblem<TGenome>[] problems)
		{
			// ReSharper disable once SwitchStatementMissingSomeCases
			switch (Interlocked.CompareExchange(ref _state, 1, 0))
			{
				case -1:
					throw new InvalidOperationException("Cannot start if cancellation requested.");

				case 0:
					if (Canceller.IsCancellationRequested)
						goto case -1;

					// ReSharper disable once ConditionIsAlwaysTrueOrFalse
					if (problems != null) foreach (var problem in problems)
							ProblemsInternal.Add(problem);

					if (!ProblemsInternal.HasAny())
						throw new InvalidOperationException("Cannot start without any registered 'Problems'");

					return StartInternal(Canceller.Token);

				case 1:
					throw new InvalidOperationException("Already started.");

			}

			throw new Exception("Unknown state");
		}

		protected abstract Task StartInternal(CancellationToken token);

		// ReSharper disable once VirtualMemberNeverOverridden.Global
		protected virtual void OnCancelled() { }

		public void Cancel()
		{
			if (-1 == Interlocked.Exchange(ref _state, -1)) return;
			Canceller.Cancel();
			OnCancelled();
		}

#if DEBUG

		protected const bool EMIT_GENOMES = false;

		// ReSharper disable once UnusedMember.Local
		protected static string GetGenomeInfo(TGenome genome)
			=> StringBuilderPool.Rent(sb =>
			{
				foreach (var logEntry in genome.Log)
				{
					sb.Append(logEntry.Category)
						.Append(" > ")
						.Append(logEntry.Message);

					var data = logEntry.Data;
					if (!string.IsNullOrWhiteSpace(data))
						sb.Append(':').AppendLine().Append(data);

					sb.AppendLine();
				}
			});

#endif
	}


}
