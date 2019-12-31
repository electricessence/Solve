using Solve.Supporting.TaskScheduling;
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
	public abstract class PushProcessingSchemeBase<TGenome> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{
		internal readonly PriorityQueueTaskScheduler Scheduler;
		readonly TaskFactory PostingFactory;

		protected PushProcessingSchemeBase(IGenomeFactory<TGenome> genomeFactory, bool runSynchronously = false)
			: base(genomeFactory)
		{
			Scheduler = new PriorityQueueTaskScheduler(TaskScheduler.Default)
			{
				Name = "ProcessingSchemeBase Scheduler"
			};
			PostingFactory = new TaskFactory(Scheduler[1]);
			_runSynchronously = runSynchronously;
		}

		private readonly bool _runSynchronously;

		protected abstract ValueTask PostAsync(TGenome genome);

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

		readonly Channel<TGenome> FactoryBuffer = Channel.CreateBounded<TGenome>(/*Environment.ProcessorCount*/ 2);

		async Task BufferGenomes(CancellationToken token)
		{
			// Buffer a couple of the available genomes...

			foreach (var genome in Factory)
			{
			retry:

				if (token.IsCancellationRequested)
					break;

				if (FactoryBuffer.Writer.TryWrite(genome))
					continue;

				if (!await FactoryBuffer.Writer.WaitToWriteAsync().ConfigureAwait(false))
					break;

				goto retry;
			}

			FactoryBuffer.Writer.Complete();
		}

		async Task PostFromBufferSingle(CancellationToken token)
		{
		retry:
			TGenome? genome = null;
			if (!token.IsCancellationRequested && !FactoryBuffer.Reader.TryRead(out genome))
				genome = Factory.Next();

			if (genome == null) return;

			var p = PostAsync(genome);
			if (p.IsCompletedSuccessfully)
				await Task.Yield();
			else
				await p.ConfigureAwait(false);
			goto retry;
		}

		Task PostFromBuffer(CancellationToken token)
			=> Task.WhenAll(
				Enumerable
				.Range(0, Environment.ProcessorCount * 4)
				.Select(s => PostingFactory
					.StartNew(() => PostFromBufferSingle(token), token).Unwrap()
					.ContinueWith(t =>
					{
						if (t.IsCanceled) return Task.CompletedTask;
						// ReSharper disable once ConvertIfStatementToReturnStatement
						if (!t.IsFaulted) return t;

#if DEBUG
						var f = t.Exception;
						Debug.Assert(f != null);
						// ReSharper disable once PossibleNullReferenceException
						Debug.Fail(f.Message, f.InnerException!.StackTrace);
#endif
						return t;
					}, token))
				.ToArray());

		async Task PostSynchronously(CancellationToken token)
		{
			//var pOptions = new ParallelOptions
			//{
			//	CancellationToken = token,
			//};
			//Parallel.ForEach(Factory, pOptions, Post);

			foreach (var f in Factory)
			{
				if (!token.IsCancellationRequested)
					await PostAsync(f).ConfigureAwait(false);
			}
		}

		protected override Task StartInternal(CancellationToken token)
			=> _runSynchronously
			? PostSynchronously(token)
			: Task.WhenAll(
				PostingFactory.StartNew(() => BufferGenomes(token), token),
				PostFromBuffer(token));
	}

}
