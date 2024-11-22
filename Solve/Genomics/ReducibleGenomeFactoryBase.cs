using App.Metrics.Counter;

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
		string? aRed = GetReduced(a)?.Hash;
		if (aRed is null) return false;
		string? bRed = GetReduced(b)?.Hash;
		return bRed is not null && aRed == bRed;
	}

	protected override IEnumerable<TGenome> GetVariationsInternal(TGenome source)
	{
		if (source is null) yield break;
		TGenome? reduced = GetReduced(source);
		if (reduced is not null && reduced != source && reduced.Hash != source.Hash)
			yield return reduced;
	}

	protected virtual TGenome? GetReduced(TGenome source) => null;
}
