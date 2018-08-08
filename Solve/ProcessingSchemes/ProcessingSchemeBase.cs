using System;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
#if DEBUG
using System.Diagnostics;
#endif


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

		readonly Channel<TGenome> FactoryBuffer = Channel.CreateBounded<TGenome>(/*Environment.ProcessorCount **/ 2);

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

		void PostFromBufferSingle(CancellationToken token)
		{
			retry:
			TGenome genome = null;
			if (!token.IsCancellationRequested && !FactoryBuffer.Reader.TryRead(out genome))
				genome = Factory.Next();

			if (genome == null) return;
			Post(genome);

			goto retry;
		}

		Task PostFromBuffer(CancellationToken token)
			=> Task.WhenAll(
				Enumerable
					.Range(0, Environment.ProcessorCount)
					.Select(s => Task.Run(() => PostFromBufferSingle(token), token)
						.ContinueWith(t =>
						{
							if (t.IsCanceled) return Task.CompletedTask;
							// ReSharper disable once ConvertIfStatementToReturnStatement
							if (!t.IsFaulted) return t;

#if DEBUG
							var f = t.Exception;
							Debug.Assert(f != null);
							// ReSharper disable once PossibleNullReferenceException
							Debug.Fail(f.Message, f.InnerException.StackTrace);
#endif
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
