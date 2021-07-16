using System;
using System.Threading.Tasks;
using System.Threading;
using System.Threading.Channels;

namespace Solve.ProcessingSchemes
{
    public class AsyncFactoryDriver<TGenome> : IFactoryDriver<TGenome>
		where TGenome : class, IGenome
	{
		private readonly IGenomeFactory<TGenome> _genomeFactory;
		private readonly ChannelWriter<TGenome> _receiver;

		protected AsyncFactoryDriver(IGenomeFactory<TGenome> genomeFactory, ChannelWriter<TGenome> receiver)
		{
			_genomeFactory = genomeFactory ?? throw new ArgumentNullException(nameof(genomeFactory));
			_receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
		}

		protected virtual async Task Run(CancellationToken token)
		{
			foreach (var f in _genomeFactory)
			{
				await _receiver.WriteAsync(f).ConfigureAwait(false);
			}
		}

		public Task Start(CancellationToken token) => Task.Run(() => Run(token));

	}
}
