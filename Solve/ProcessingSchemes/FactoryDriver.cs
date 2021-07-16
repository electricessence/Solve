using System;
using System.Threading.Tasks;
using System.Threading;
using Open.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
    public class FactoryDriver<TGenome> : BroadcasterBase<TGenome>, IFactoryDriver<TGenome>
		where TGenome : class, IGenome
	{
		private readonly IGenomeFactory<TGenome> _genomeFactory;

		protected FactoryDriver(IGenomeFactory<TGenome> genomeFactory)
		{
			_genomeFactory = genomeFactory ?? throw new ArgumentNullException(nameof(genomeFactory));
		}

		protected virtual void Run(CancellationToken token)
		{
			foreach (var f in _genomeFactory)
			{
				if (token.IsCancellationRequested) break;
				Broadcast(f);
			}
		}

		public Task Start(CancellationToken token)
		{
			if (!HasObservers) throw new InvalidOperationException("Factory driver has no observers.");
			return Task.Run(() => Run(token))
			.OnFaulted(Fault)
			.OnFullfilled(Complete);
		}

	}
}
