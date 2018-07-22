using Open.Evaluation.Core;
using Open.Hierarchy;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Solve.Evaluation
{
	using IGene = IEvaluate<double>;

	public class EvalGenome : GenomeBase, IHaveRoot<IGene>
	{
		public EvalGenome(IGene root)
		{
			SetRoot(root);
		}

		public IGene Root { get; private set; }
		object IHaveRoot.Root => Root;

		public bool SetRoot(IGene root)
		{
			AssertIsNotFrozen();
			if (Root == root) return false;
			Root = root;
			return true;
		}

		protected override int GetGeneCount() => Root is IParent r ? r.GetNodes().Count() : (Root == null ? 0 : 1);
		protected override string GetHash() => Root.ToStringRepresentation();

		public new EvalGenome Clone()
		{
#if DEBUG
			var clone = new EvalGenome(Root);
			clone.AddLogEntry("Origin", "Cloned");
			return clone;
#else
			return new EvalGenome(Root);
#endif
		}

		protected override object CloneInternal() => Clone();

		protected override void OnBeforeFreeze()
		{
			if (Root == null)
				throw new InvalidOperationException("Cannot freeze genome without a root.");
		}


		public double Evaluate(IReadOnlyList<double> values)
			=> Root.Evaluate(values);

		public string ToAlphaParameters()
			=> AlphaParameters.ConvertTo(Hash);
	}
}
