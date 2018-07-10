﻿using Open.Hierarchy;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Solve.Evaluation
{
	using IGene = Open.Evaluation.Core.IEvaluate<double>;

	public class EvalGenome : ReducibleGenomeBase<EvalGenome>, IHaveRoot<IGene>
	{
		public EvalGenome(IGene root) : base()
		{
			SetRoot(root);
		}

		public IGene Root { get; private set; }
		object IHaveRoot.Root => Root;

		bool SetRoot(IGene root)
		{
			AssertIsNotFrozen();
			if (Root != root)
			{
				Root = root;
				return true;
			}
			return false;
		}


		protected override int GetGeneCount() => Root is IParent r ? r.GetNodes().Count() : (Root == null ? 0 : 1);
		protected override string GetHash() => Root.ToStringRepresentation();

		public new EvalGenome Clone() => new EvalGenome(Root);

		protected override object CloneInternal() => Clone();

		protected override void OnBeforeFreeze()
		{
			if (Root == null)
				throw new InvalidOperationException("Cannot freeze genome without a root.");

			base.OnBeforeFreeze();
		}


		public double Evaluate(IReadOnlyList<double> values)
			=> Root.Evaluate(values);


		public string ToAlphaParameters(bool reduced = false)
			=> AlphaParameters.ConvertTo(reduced ? AsReduced().Hash : Hash);

		protected override EvalGenome Reduction()
		{
			throw new NotImplementedException();
		}
	}
}