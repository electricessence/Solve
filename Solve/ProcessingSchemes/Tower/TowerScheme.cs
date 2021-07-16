using Solve.Metrics;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
    public partial class TowerScheme<TGenome> : TowerSchemeBase<TGenome>
        where TGenome : class, IGenome
    {
        public TowerScheme(
            IGenomeFactory<TGenome> genomeFactory,
            SchemeConfig config,
            GenomeProgressionLog? genomeProgressionLog = null)
            : base(genomeFactory, config, genomeProgressionLog)
        {
        }

        public TowerScheme(
            IGenomeFactory<TGenome> genomeFactory,
            SchemeConfig.PoolSizing config,
            GenomeProgressionLog? genomeProgressionLog = null)
            : base(genomeFactory, new SchemeConfig { PoolSize = config }, genomeProgressionLog)
        {
        }

        IEnumerable<ProblemTower>? ActiveTowers;

        protected async ValueTask<int> PostAsync(TGenome genome)
        {
            if (genome is null) throw new ArgumentNullException(nameof(genome));
            Contract.EndContractBlock();

            int count = 0;
            foreach (var t in ActiveTowers!)
            {
                await t.PostAsync(genome).ConfigureAwait(false);
                ++count;
            }
            return count;
        }

        protected override async Task StartInternal(CancellationToken token)
        {
            var towers = Problems.Select(p => new ProblemTower(Config, p, Factory)).ToList();
            //Towers = towers.AsReadOnly();
            ActiveTowers = towers.Where(t => !t.Problem.HasConverged);

        retry:

            TGenome? genome = null;
            if (!token.IsCancellationRequested)
                genome = Factory.Next();

            if (genome == null) return;

            GenomeProgress?[genome.Hash].Add(GenomeEvent.EventType.Born);
            if(await PostAsync(genome)==0) return;

            goto retry;
        }
    }
}
