using System;
using System.Collections.Generic;
using System.Threading;
using Open.Collections;
using Solve;
using EvaluationFramework;
using System.Linq;
using IGene = EvaluationFramework.IEvaluate<System.Collections.Generic.IReadOnlyList<double>, double>;

namespace BlackBoxFunction
{
	public sealed class Genome : ReducibleGenomeBase<IGene, Genome>
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

		protected override IGene[] GetGenes()
		{
			var descendants = (Root as IParent<IGene>).Descendants;
			var root = new IGene[] { Root };
			return descendants == null ? root : root.Concat(descendants).ToArray();
		}

		protected override void OnBeforeFreeze()
		{
			if (Root == null)
				throw new InvalidOperationException("Cannot freeze genome without a root.");
			_hash = Root.ToStringRepresentation();
		}
		
		static IParent<IGene> FindParent(IGene parent, IGene child)
		{
			var p = parent as IParent<IGene>;
			if (p!=null)
			{
				foreach(var c in p.Children)
				{
					if (child == c) return p;
					var np = FindParent(c,child);
					if (np != null) return np;
				}
			}
			return null;
		}

		public IParent<IGene> FindParent(IGene child)
		{
			return FindParent(Root,child);
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
			IGenome result;
			e.ConcurrentTryMoveNext(out result);
			return result;
		}

		IEnumerator<Genome> _mutationEnumerator;
		public IGenome NextMutation()
		{
			var source = _mutationEnumerator;
			if (source == null) return null;
			IGenome result;
			source.ConcurrentTryMoveNext(out result);
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
				Expansions_Array = Lazy.New(() => Expansions.ToArrayDirect());
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
