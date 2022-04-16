/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using Open.Collections;
using Open.Disposable;
using Solve.Metrics;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve;

// Defines the pipeline?
// ReSharper disable once PossibleInfiniteInheritance
public abstract class EnvironmentBase<TGenome>
	: BroadcasterBase<(TGenome Genome, Fitness, IProblem<TGenome> Problem, int PoolIndex)>, IEnvironment<TGenome>
	where TGenome : class, IGenome
{
	protected internal readonly IGenomeFactory<TGenome> Factory;

	protected readonly List<IProblem<TGenome>> ProblemsInternal;
	public IReadOnlyList<IProblem<TGenome>> Problems { get; }

	protected EnvironmentBase(
		IGenomeFactory<TGenome> genomeFactory,
		GenomeProgressionLog? genomeProgressionLog = null)
	{
		Factory = genomeFactory ?? throw new ArgumentNullException(nameof(genomeFactory));
		Contract.EndContractBlock();

		GenomeProgress = genomeProgressionLog;
		ProblemsInternal = new List<IProblem<TGenome>>();
		Problems = ProblemsInternal.AsReadOnly();
	}

	public void AddProblem(IProblem<TGenome> problem)
	{
		if (problem is null)
			throw new ArgumentNullException(nameof(problem));
		Contract.EndContractBlock();

		if (_state != 0)
			throw new InvalidOperationException("Attempting to add a problem when the environment has already started.");

		ProblemsInternal.Add(problem);
	}

	protected readonly CancellationTokenSource Canceller = new();

	public CancellationToken CancellationToken => Canceller.Token;

	protected GenomeProgressionLog? GenomeProgress { get; }

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

				if (problems is not null)
					ProblemsInternal.AddRange(problems);

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
		=> StringBuilderPool.RentToString(sb =>
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

	public bool HaveAllProblemsConverged => Problems.All(p => p.HasConverged);
}