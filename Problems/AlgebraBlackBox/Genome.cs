using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Open.Collections;
using Open.Threading;
using Solve;
using EvaluationFramework;

namespace AlgebraBlackBox
{
	public sealed class Genome : GenomeBase<IGene>, ICloneable<Genome>, IReducible<Genome>
	{
		public Genome(IGene root) : base()
		{
			Root = root;
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
				Root = root;
				return true;
			}
			return false;
		}

		void IReducible<Genome>.ReplaceReduced(Genome reduced)
		{
			throw new NotImplementedException();
		}

		protected override IGene[] GetGenes()
		{
			throw new NotImplementedException();
		}

		protected override object CloneInternal()
		{
			throw new NotImplementedException();
		}

		protected override void OnBeforeFreeze()
		{
			throw new NotImplementedException();
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

		public bool Replace(IGene target, IGene replacement)
		{
			if (target != replacement)
			{

				if (Root == target)
				{
					return SetRoot(replacement);
				}
				else
				{
					var parent = FindParent(target);
					if (parent == null)
						throw new ArgumentException("'target' not found.");
					return parent.ReplaceChild(target, replacement);
				}
			}
			return false;
		}


		public new Genome Clone()
		{
			return new Genome(Root.Clone());
		}

		public double Evaluate(IReadOnlyList<double> values)
		{
			return Root.Evaluate(values);
		}


		public string ToAlphaParameters(bool reduced = false)
		{
			return AlphaParameters.ConvertTo(reduced ? AsReduced().Hash : Hash);
		}

		Lazy<Genome> _reduced;

		public bool IsReducible
		{
			get
			{
				return _reduced.Value != this;
			}
		}

		public Genome AsReduced(bool ensureClone = false)
		{

			var r = _reduced.Value;
			if (ensureClone)
			{
				r = r.Clone();
				r.Freeze();
			}
			return r;
		}

		internal void ReplaceReduced(Genome reduced)
		{
			if (AsReduced() != reduced)
				Interlocked.Exchange(ref _reduced, Lazy.New(() => reduced));
		}

		public override IGenome NextVariation()
		{
			var source = _variations;
			if (source == null) return null;
			var e = LazyInitializer.EnsureInitialized(ref _variationEnumerator, () => source.GetEnumerator());
			IGenome result;
			e.ConcurrentTryMoveNext(out result);
			return result;
		}

		IEnumerator<Genome> _mutationEnumerator;
		public override IGenome NextMutation()
		{
			var source = _mutationEnumerator;
			if (source == null) return null;
			IGenome result;
			source.ConcurrentTryMoveNext(out result);
			return result;
		}

		IEnumerator<Genome> _variationEnumerator;
		LazyList<Genome> _variations;
		public override IReadOnlyList<IGenome> Variations
		{
			get
			{
				return _variations;
			}
		}

		public override string Hash
		{
			get
			{
				throw new NotImplementedException();
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

		

	}
}
