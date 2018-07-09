using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
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

		protected async override Task StartInternal(CancellationToken token)
		{
			foreach (var f in Factory)
			{
				if (!token.IsCancellationRequested)
					await PostAsync(f).ConfigureAwait(false);
			}
		}

	}
}
