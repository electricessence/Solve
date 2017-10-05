using System;
using System.Collections.Generic;
using System.Threading;
using Open.Collections;
using Solve;
using Open.Evaluation;
using System.Linq;
using IGene = Open.Evaluation.IEvaluate<double>;

namespace BlackBoxFunction
{
	public sealed class Genome : ReducibleGenomeBase<Genome>
	{
		public Genome(IGene root) : base()
		{
			SetRoot(root);
		}

		public IGene Root
		{
			get;
			private set;
		}

		bool SetRoot(IGene root)
		{
			if (Root != root)
			{
				_hash = null;
				Root = root;
				return true;
			}
			return false;
		}

		string _hash;
		public override string Hash
		{
			get
			{
				return _hash ?? Root.ToStringRepresentation();
			}
		}

		public new Genome Clone()
		{
			return new Genome(this.Root);
		}

		protected override object CloneInternal()
		{
			return this.Clone();
		}

		// To avoid memory bloat, we don't retain the hierarchy.
		public Hierarchy.Node<IGene> GetGeneHierarchy()
		{
			return Hierarchy.Get<IGene,IGene>(Root);
		}

		protected override void OnBeforeFreeze()
		{
			if (Root == null)
				throw new InvalidOperationException("Cannot freeze genome without a root.");
			_hash = Root.ToStringRepresentation();
		}

	
		public double Evaluate(IReadOnlyList<double> values)
		{
			return Root.Evaluate(values);
		}


		public string ToAlphaParameters(bool reduced = false)
		{
			return AlphaParameters.ConvertTo(reduced ? AsReduced().Hash : Hash);
		}

		public IGenome NextVariation()
		{
			var source = _variations;
			if (source == null) return null;
			var e = LazyInitializer.EnsureInitialized(ref _variationEnumerator, () => source.GetEnumerator());
            e.ConcurrentTryMoveNext(out IGenome result);
            return result;
		}

		IEnumerator<Genome> _mutationEnumerator;
		public IGenome NextMutation()
		{
			var source = _mutationEnumerator;
			if (source == null) return null;
            source.ConcurrentTryMoveNext(out IGenome result);
            return result;
		}

		IEnumerator<Genome> _variationEnumerator;
		LazyList<Genome> _variations;
		public IReadOnlyList<IGenome> Variations
		{
			get
			{
				return _variations;
			}
		}
		
		internal void RegisterVariations(IEnumerable<Genome> variations)
		{
			_variations = variations.Memoize();
		}

		internal void RegisterMutations(IEnumerable<Genome> mutations)
		{
			_mutationEnumerator = mutations.GetEnumerator();
		}

		ConcurrentHashSet<string> Expansions = new ConcurrentHashSet<string>();
		Lazy<string[]> Expansions_Array;
		public bool RegisterExpansion(string genomeHash)
		{
			var added = Expansions.Add(genomeHash);
			if (added)
				Expansions_Array = Lazy.Create(() => Expansions.ToArrayDirect());
			return added;
		}

		public string[] GetExpansions()
		{
			return Expansions_Array?.Value;
		}

		protected override Genome Reduction()
		{
			throw new NotImplementedException();
		}
	}
}
