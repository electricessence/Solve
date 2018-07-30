using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	// ReSharper disable once PossibleInfiniteInheritance
	public abstract class ProcessingSchemeBase<TGenome> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{
		protected ProcessingSchemeBase(IGenomeFactory<TGenome> genomeFactory, bool runSynchronously = false)
			: base(genomeFactory)
		{
			_runSynchronously = runSynchronously;
		}

		private readonly bool _runSynchronously;

		protected abstract void Post(TGenome genome);

		protected abstract Task PostAsync(TGenome genome, CancellationToken token);

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

		readonly Channel<TGenome> FactoryBuffer = Channel.CreateBounded<TGenome>(Environment.ProcessorCount * 2);

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

		async Task PostFromBufferSingle(CancellationToken token)
		{
			retry:
			TGenome genome = null;
			if (!token.IsCancellationRequested && !FactoryBuffer.Reader.TryRead(out genome))
				genome = Factory.Next();

			if (genome == null) return;
			await PostAsync(genome, token).ConfigureAwait(false);

			goto retry;
		}

		Task PostFromBuffer(CancellationToken token)
			=> Task.WhenAll(
				Enumerable
					.Range(0, Environment.ProcessorCount)
					.Select(s => PostFromBufferSingle(token).ContinueWith(t =>
					{
						if (t.IsCanceled) return Task.CompletedTask;
						if (!t.IsFaulted) return t;

						var f = t.Exception;
						Debug.Assert(f != null);
						// ReSharper disable once PossibleNullReferenceException
						Debug.Fail(f.Message, f.InnerException.StackTrace);

						return t;
					}, token)));

		Task PostSynchronously(CancellationToken token)
			=> Task.Run(() =>
			{
				//var pOptions = new ParallelOptions
				//{
				//	CancellationToken = token,
				//};
				//Parallel.ForEach(Factory, pOptions, Post);

				foreach (var f in Factory)
				{
					if (!token.IsCancellationRequested)
						Post(f);
				}
			});

		protected override Task StartInternal(CancellationToken token)
			=> _runSynchronously
			? PostSynchronously(token)
			: Task.WhenAll(
				BufferGenomes(token),
				PostFromBuffer(token));
	}

}
