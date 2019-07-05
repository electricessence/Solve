using System;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	public class SimpleProcessingScheme<TGenome> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{
		public SimpleProcessingScheme(
			IGenomeFactory<TGenome> genomeFactory)
			:base(genomeFactory)
		{

		}

		protected override Task StartInternal(CancellationToken token)
		{
			throw new NotImplementedException();
		}
	}
}
