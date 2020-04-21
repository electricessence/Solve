using App.Metrics.Counter;
using System;
using System.Collections.Generic;

namespace Solve
{
	public abstract class ReducibleGenomeFactoryBase<TGenome> : GenomeFactoryBase<TGenome>
		where TGenome : class, IGenome
	{
		protected ReducibleGenomeFactoryBase(IProvideCounterMetrics metrics) : base(metrics)
		{ }

		protected ReducibleGenomeFactoryBase(IProvideCounterMetrics metrics, IEnumerable<TGenome>? seeds) : base(metrics, seeds)
		{ }

		public override TGenome[] AttemptNewCrossover(TGenome a, TGenome b, byte maxAttempts = 3)
		{
			if (a == null || b == null
				// Avoid inbreeding. :P
				|| a == b
				|| GetReduced(a)?.Hash == GetReduced(b)?.Hash)
				return Array.Empty<TGenome>();

			return base.AttemptNewCrossover(a, b, maxAttempts);
		}

		protected override IEnumerable<TGenome> GetVariationsInternal(TGenome source)
		{
			if (source == null) yield break;
			var reduced = GetReduced(source);
			if (reduced != null && reduced != source && reduced.Hash != source.Hash)
				yield return reduced;
		}

		protected virtual TGenome? GetReduced(TGenome source) => null;
	}
}
