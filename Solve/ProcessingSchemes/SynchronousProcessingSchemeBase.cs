using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	// ReSharper disable once PossibleInfiniteInheritance
	public abstract class SynchronousProcessingSchemeBase<TGenome> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{
		protected SynchronousProcessingSchemeBase(IGenomeFactory<TGenome> genomeFactory)
			: base(genomeFactory)
		{
		}

		protected abstract void Post(TGenome genome);

		protected abstract Task PostAsync(TGenome genome);

		//protected override Task StartInternal(CancellationToken token)
		//{
		//	var pOptions = new ParallelOptions
		//	{
		//		CancellationToken = token,
		//	};

		//	return Task.Run(cancellationToken: token, action: () =>
		//	{
		//		//Parallel.ForEach(Factory, pOptions, Post);

		//		foreach (var f in Factory)
		//		{
		//			if (!token.IsCancellationRequested)
		//				Post(f);
		//		}
		//	});
		//}

		readonly Channel<TGenome> FactoryBuffer = Channel.CreateBounded<TGenome>(System.Environment.ProcessorCount * 2);

		async Task BufferGenomes(CancellationToken token)
		{
			// Buffer a couple of the available genomes...

			foreach (var genome in Factory)
			{
				if (token.IsCancellationRequested)
					break;

				while (!FactoryBuffer.Writer.TryWrite(genome))
					await FactoryBuffer.Writer.WaitToWriteAsync(token).ConfigureAwait(false);
			}

			FactoryBuffer.Writer.Complete();
		}

		async Task PostFromBufferSingle()
		{
			retry:
			if (!FactoryBuffer.Reader.TryRead(out var genome))
				genome = Factory.Next();

			if (genome == null) return;
			await PostAsync(genome).ConfigureAwait(false);

			goto retry;
		}

		Task PostFromBuffer()
			=> Task.WhenAll(
				Enumerable
					.Range(0, System.Environment.ProcessorCount)
					.Select(s => PostFromBufferSingle()));

		protected override Task StartInternal(CancellationToken token)
			=> Task.WhenAll(
				BufferGenomes(token),
				PostFromBuffer());

	}

}
