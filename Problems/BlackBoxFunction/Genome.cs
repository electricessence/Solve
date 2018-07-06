using Open.Collections;
using Open.Collections.Synchronized;
using Open.Hierarchy;
using Solve;
using System;
using System.Collections.Generic;
using System.Threading;
using IGene = Open.Evaluation.Core.IEvaluate<double>;

namespace BlackBoxFunction
{
	public sealed class Genome : ReducibleGenomeBase<Genome>, IHaveRoot<IGene>
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

		object IHaveRoot.Root => Root;

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
			return new Genome(Root);
		}

		protected override object CloneInternal()
		{
			return Clone();
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
			LazyInitializer.EnsureInitialized(
				ref _variationEnumerator, source.GetEnumerator)
				.ConcurrentTryMoveNext(out IGenome result);
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

		public override int Length => throw new NotImplementedException();

		internal void RegisterVariations(IEnumerable<Genome> variations)
		{
			_variations = variations.Memoize();
		}

		internal void RegisterMutations(IEnumerable<Genome> mutations)
		{
			_mutationEnumerator = mutations.GetEnumerator();
		}

		LockSynchronizedHashSet<string> Expansions = new LockSynchronizedHashSet<string>();
		Lazy<string[]> Expansions_Array;
		public bool RegisterExpansion(string genomeHash)
		{
			var added = Expansions.Add(genomeHash);
			if (added)
				Expansions_Array = Lazy.Create(() => Expansions.Snapshot());
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
