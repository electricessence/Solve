using Open.Collections.Synchronized;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.Schemes
{
	public class Classic<TGenome> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{
		public readonly ReadWriteSynchronizedList<GenomeFitness<TGenome>[]> Pools;
		public readonly ushort PoolSize;
		public Classic(IGenomeFactory<TGenome> genomeFactory, ushort poolSize) : base(genomeFactory)
		{
			PoolSize = poolSize;
			Pools = new ReadWriteSynchronizedList<GenomeFitness<TGenome>[]>();
		}

		protected override Task StartInternal(CancellationToken token)
		{
			throw new NotImplementedException();
		}
	}
}
