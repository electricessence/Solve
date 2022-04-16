using System;
using System.Diagnostics.Contracts;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

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

		readonly Subject<int> _levelCreated = new();
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

		public ValueTask PostAsync(TGenome next)
		{
			if (next is null) throw new ArgumentNullException(nameof(next));
			Contract.EndContractBlock();

			return Root.PostAsync(new LevelProgress<TGenome>(next, ((ITower<TGenome>)this).NewFitness()));
		}
	}
}
