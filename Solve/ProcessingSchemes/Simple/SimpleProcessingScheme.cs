using Open.Collections.Synchronized;
using System;
using System.Collections.Generic;
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

		readonly LockSynchronizedLinkedList<IList<(TGenome Genome, Fitness[] Fitness)>> Population
			= new LockSynchronizedLinkedList<IList<(TGenome Genome, Fitness[] Fitness)>>();

		protected override Task StartInternal(CancellationToken token)
		{
			throw new NotImplementedException();
		}
	}
}
