using Open.Hierarchy;
using Solve;
using System;
using System.Collections.Generic;
using IGene = Open.Evaluation.Core.IEvaluate<double>;

namespace BlackBoxFunction
{
	public sealed class Genome : ReducibleGenomeBase<Genome>, IHaveRoot<IGene>
	{
		public Genome(in IGene root) : base()
		{
			SetRoot(in root);
		}

		private IGene _root;
		public IGene Root => _root;

		object IHaveRoot.Root => _root;

		bool SetRoot(in IGene root)
		{
			if (_root != root)
			{
				_hash = null;
				_root = root;
				return true;
			}
			return false;
		}


		public override int Length => throw new NotImplementedException();

		string _hash;
		public override string Hash => _hash ?? Root.ToStringRepresentation();

		public new Genome Clone() => new Genome(in _root);

		protected override object CloneInternal() => Clone();

		protected override void OnBeforeFreeze()
		{
			if (_root == null)
				throw new InvalidOperationException("Cannot freeze genome without a root.");

			_hash = _root.ToStringRepresentation();
		}


		public double Evaluate(in IReadOnlyList<double> values)
			=> _root.Evaluate(values);


		public string ToAlphaParameters(bool reduced = false)
			=> AlphaParameters.ConvertTo(reduced ? AsReduced().Hash : Hash);

		protected override Genome Reduction()
		{
			throw new NotImplementedException();
		}
	}
}
