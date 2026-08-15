using System.Diagnostics.Contracts;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Channels;

namespace Solve.ProcessingSchemes;

public partial class TowerScheme<TGenome>
{
	protected class ProblemTower
		: BroadcasterBase<(TGenome Genome, Fitness, IProblem<TGenome> Problem, int PoolIndex)>, ITower<TGenome>
	{
		public SchemeConfig.Values Config { get; }
		public IProblem<TGenome> Problem { get; }

		public IGenomeFactory<TGenome> Factory { get; }

		/// <summary>
		/// The scheme's cancellation token (see <see cref="EnvironmentBase{TGenome}.CancellationToken"/>),
		/// threaded through every blocking channel operation this tower owns — the shared
		/// evaluation queue below, and (via this same property, read from <see cref="Level"/>)
		/// each level's own Pool channel — so a cancel issued while the pipeline is congested
		/// unblocks those awaits immediately instead of waiting indefinitely for capacity that
		/// the congestion itself is withholding.
		/// </summary>
		public CancellationToken CancellationToken { get; }

		protected Level Root { get; }

		private readonly Subject<int> _levelCreated = new();
		public IObservable<int> LevelCreated { get; }
		internal void OnLevelCreated(int level)
		{
			_levelCreated.OnNext(level);
			if (Config.MaxLevels - 1 == level) _levelCreated.OnCompleted();
		}

		// A dispatched unit of work: evaluate `Contender` at `Level`. A plain value-type
		// tuple rather than a Func<ValueTask> closure — the dispatch path is the hottest
		// path in the tower, and this avoids a per-submission heap allocation there.
		private readonly record struct EvaluationRequest(Level Level, LevelProgress<TGenome> Contender);

		// Shared bounded evaluation stage for this tower: every level's PostAsync submits
		// its (evaluate + merge + pool-or-promote) unit of work here instead of running it
		// inline on the submitter's own await chain. A fixed number of worker loops
		// (Config.MaxConcurrentEvaluations, default Environment.ProcessorCount) pull from
		// this single queue, so evaluation fan-out no longer depends on how many levels
		// happen to exist yet. The queue's bounded capacity is what supplies backpressure:
		// once full, submitters (the producer loop, or a level's pool reader promoting
		// winners) await capacity rather than growing an unbounded backlog in memory.
		//
		// Only the "external" entry points (ProblemTower.PostAsync for the producer, and
		// Level.PromoteAsync for a pool reader) submit through this queue with the blocking
		// DispatchAsync. A level's own fast-track continuation into NextLevel prefers the
		// non-blocking TryDispatch instead (see Level.ContinueAsync) so a worker never risks
		// blocking a write to the very queue only workers themselves drain — that could
		// deadlock under load — falling back to continuing in-process when the queue is
		// momentarily full.
		private readonly Channel<EvaluationRequest> _evaluationQueue;
		private readonly Task[] _evaluationWorkers;

		public ProblemTower(
			SchemeConfig.Values config,
			IProblem<TGenome> problem,
			IGenomeFactory<TGenome> factory,
			CancellationToken cancellationToken)
		{
			Config = config;
			Problem = problem ?? throw new ArgumentNullException(nameof(problem));
			Factory = factory ?? throw new ArgumentNullException(nameof(factory));
			CancellationToken = cancellationToken;

			int degreeOfParallelism = Math.Max(1, config.MaxConcurrentEvaluations);
			_evaluationQueue = Channel.CreateBounded<EvaluationRequest>(new BoundedChannelOptions(degreeOfParallelism * 4)
			{
				SingleReader = false,
				SingleWriter = false,
				FullMode = BoundedChannelFullMode.Wait,
				// Synchronous continuations fuse a worker's next iteration onto whatever
				// thread completed the previous unit of work, degrading thread-pool
				// fairness under load (same rationale as the per-level Pool channel).
				AllowSynchronousContinuations = false
			});
			_evaluationWorkers = new Task[degreeOfParallelism];
			for (int i = 0; i < degreeOfParallelism; i++)
				_evaluationWorkers[i] = Task.Run(RunEvaluationWorkerAsync);

			Root = new(0, this);
			LevelCreated = _levelCreated.AsObservable();
			Contract.EndContractBlock();
		}

		private async Task RunEvaluationWorkerAsync()
		{
			ChannelReader<EvaluationRequest> reader = _evaluationQueue.Reader;
			try
			{
				// Threading the tower's cancellation token through here means a cancel
				// unblocks a worker that's idly waiting for the next item instead of
				// leaving it parked forever (the queue is never Complete()-d, so absent
				// this token an idle worker would otherwise wait indefinitely).
				await foreach (EvaluationRequest request in reader.ReadAllAsync(CancellationToken).ConfigureAwait(false))
				{
					// ProcessContenderSafelyAsync reports its own failures via OnLevelFault and
					// does not rethrow (including OperationCanceledException, which it treats
					// as a normal shutdown signal), so nothing further to catch here.
					await request.Level.ProcessContenderSafelyAsync(request.Contender).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException)
			{
				// Normal shutdown: the tower's cancellation token was signaled while this
				// worker was waiting on the shared evaluation queue. Not a fault.
			}
		}

		/// <summary>
		/// Submits a unit of evaluation work to this tower's shared bounded worker stage.
		/// Completes once the work is accepted onto the queue (awaiting capacity when the
		/// queue is full) — NOT once the work itself has finished running. Honors the
		/// tower's cancellation token so a submitter parked here by a congested queue
		/// (the producer loop via <see cref="PostAsync"/>, or a level's pool reader via
		/// <see cref="Level.PromoteAsync"/>) is released promptly on cancel instead of
		/// waiting indefinitely for capacity the congestion itself is withholding.
		/// </summary>
		internal ValueTask DispatchAsync(Level level, LevelProgress<TGenome> contender)
			=> _evaluationQueue.Writer.WriteAsync(new EvaluationRequest(level, contender), CancellationToken);

		/// <summary>
		/// Non-blocking variant of <see cref="DispatchAsync"/>: attempts to hand the work off
		/// to the shared queue without ever waiting for capacity. Returns
		/// <see langword="false"/> immediately when the queue is momentarily full instead of
		/// blocking, so a caller that is itself one of this tower's evaluation workers (see
		/// <c>Level.ContinueAsync</c>) can safely try to redistribute work to another worker
		/// without risking a self-deadlock on this very queue.
		/// </summary>
		internal bool TryDispatch(Level level, LevelProgress<TGenome> contender)
			=> _evaluationQueue.Writer.TryWrite(new EvaluationRequest(level, contender));

		protected override void OnDispose()
		{
			base.OnDispose();
			_levelCreated.Dispose();
		}

		public void Broadcast(LevelProgress<TGenome> progress, int poolIndex)
			=> Broadcast((progress.Genome, progress.Fitnesses[poolIndex], Problem, poolIndex));

		/// <summary>
		/// Surfaces a level's processing failure instead of letting the level die silently
		/// (which would stall the tower with no signal). Faults the broadcast so observers
		/// receive OnError.
		/// </summary>
		internal void OnLevelFault(int level, Exception exception)
		{
			Console.Error.WriteLine("Level {0}.{1} processing fault: {2}", Problem.ID, level, exception);
			Fault(exception);
		}

#pragma warning disable IDE0079 // Remove unnecessary suppression
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "<Pending>")]
#pragma warning restore IDE0079 // Remove unnecessary suppression
		public ValueTask PostAsync(TGenome next)
		{
			ArgumentNullException.ThrowIfNull(next);
			Contract.EndContractBlock();

			return Root.PostAsync(new LevelProgress<TGenome>(next, ((ITower<TGenome>)this).NewFitness()));
		}
	}
}
