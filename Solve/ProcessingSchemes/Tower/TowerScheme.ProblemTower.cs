using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
    public partial class TowerScheme<TGenome>
    {
        protected class ProblemTower
            : BroadcasterBase<(TGenome Genome, Fitness, IProblem<TGenome> Problem, int PoolIndex)>, ITower<TGenome>
        {
            public SchemeConfig.Values Config { get; }
            public IProblem<TGenome> Problem { get; }

            public IGenomeFactory<TGenome> Factory { get; }

            protected Level Root { get; }

            public ProblemTower(
                SchemeConfig.Values config,
                IProblem<TGenome> problem,
                IGenomeFactory<TGenome> factory)
            {
                Config = config;
                Problem = problem ?? throw new ArgumentNullException(nameof(problem));
                Factory = factory ?? throw new ArgumentNullException(nameof(factory));
                Root = new(0, this);
                Contract.EndContractBlock();
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
}
