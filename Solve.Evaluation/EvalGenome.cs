using Open.Evaluation.Core;
using Open.Hierarchy;

namespace Solve.Evaluation;

public class EvalGenome<T>(IEvaluate<T> root) : GenomeBase, IHaveRoot<IEvaluate<T>>
{
	public IEvaluate<T> Root { get; private set; } = root;
	object IHaveRoot.Root => Root;

	public bool SetRoot(IEvaluate<T> root)
	{
		AssertIsNotFrozen();
		if (Root == root) return false;
		Root = root;
		return true;
	}

	protected override int GetGeneCount() => Root is IParent r ? r.GetNodes().Count() : (Root is null ? 0 : 1);
	protected override string GetHash() => Root.ToStringRepresentation();

	public new EvalGenome<T> Clone()
	{
#if DEBUG
		var clone = new EvalGenome<T>(Root);
		clone.AddLogEntry("Origin", "Cloned");
		return clone;
#else
		return new(Root);
#endif
	}

	protected override object CloneInternal() => Clone();

	protected override void OnBeforeFreeze()
	{
		if (Root is null)
			throw new InvalidOperationException("Cannot freeze genome without a root.");
	}

	public T Evaluate(IReadOnlyList<T> values)
		=> Root.Evaluate(values);

	public string ToAlphaParameters()
		=> AlphaParameters.ConvertTo(Hash);
}
