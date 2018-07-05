using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	public abstract class SynchronousProcessingSchemeBase<TGenome> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{
		protected SynchronousProcessingSchemeBase(in IGenomeFactory<TGenome> genomeFactory)
			: base(genomeFactory)
		{
		}

		protected abstract void Post(in TGenome genome);

		protected override Task StartInternal(in CancellationToken token)
		{
			var pOptions = new ParallelOptions
			{
				CancellationToken = token,
			};

			return Task.Run(cancellationToken: token, action: () =>
			{
				Parallel.ForEach(Factory, pOptions, g => Post(in g));

				//foreach (var f in Factory)
				//{
				//	if (!token.IsCancellationRequested)
				//		Post(f);
				//}
			});
		}

	}
}
