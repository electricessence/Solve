using System.Diagnostics.Contracts;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Solve.ProcessingSchemes;

public partial class TowerScheme<TGenome>
{
	protected class ProblemTower
		: BroadcasterBase<(TGenome Genome, Fitness, IProblem<TGenome> Problem, int PoolIndex)>, ITower<TGenome>
	{
		public SchemeConfig.Values Config { get; }
		public IProblem<TGenome> Problem { get; }

		public IGenomeFactory<TGenome> Factory { get; }

		protected Level Root { get; }

		private readonly Subject<int> _levelCreated = new();
		public IObservable<int> LevelCreated { get; }
		internal void OnLevelCreated(int level)
		{
			_levelCreated.OnNext(level);
			if (Config.MaxLevels - 1 == level) _levelCreated.OnCompleted();
			Console.WriteLine("Level Created: {0}.{1}", Problem.ID, level);
		}

		public ProblemTower(
			SchemeConfig.Values config,
			IProblem<TGenome> problem,
			IGenomeFactory<TGenome> factory)
		{
			Config = config;
			Problem = problem ?? throw new ArgumentNullException(nameof(problem));
			Factory = factory ?? throw new ArgumentNullException(nameof(factory));
			Root = new(0, this);
			LevelCreated = _levelCreated.AsObservable();
			Contract.EndContractBlock();
		}

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
