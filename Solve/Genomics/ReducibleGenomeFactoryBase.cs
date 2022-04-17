using App.Metrics.Counter;
using System;
using System.Collections.Generic;

namespace Solve;

public abstract class ReducibleGenomeFactoryBase<TGenome> : GenomeFactoryBase<TGenome>
	where TGenome : class, IGenome
{
	protected ReducibleGenomeFactoryBase(IProvideCounterMetrics metrics)
		: base(metrics) { }

	protected ReducibleGenomeFactoryBase(IProvideCounterMetrics metrics, IEnumerable<TGenome>? seeds)
		: base(metrics, seeds) { }

	protected override bool CannotCrossover(TGenome a, TGenome b)
	{
		if (base.CannotCrossover(a, b)) return true;
		var aRed = GetReduced(a)?.Hash;
		if (aRed is null) return false;
		var bRed = GetReduced(b)?.Hash;
		return bRed is not null && aRed == bRed;
	}

	protected override IEnumerable<TGenome> GetVariationsInternal(TGenome source)
	{
		if (source is null) yield break;
		var reduced = GetReduced(source);
		if (reduced is not null && reduced != source && reduced.Hash != source.Hash)
			yield return reduced;
	}

	protected virtual TGenome? GetReduced(TGenome source) => null;
}
