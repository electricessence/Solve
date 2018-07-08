using Open.Cloneable;
using Solve;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Eater
{
	[DebuggerDisplay("{_genes.Length}:{_hash}")]
	public sealed class Genome
		: ReducibleGenomeBase<Genome>, ICloneable<Genome>, IEnumerable<Step>
	{

		static readonly Step[] EMPTY = System.Array.Empty<Step>();

		public Genome() : base()
		{
			_genes = EMPTY;
		}

		public Genome(IEnumerable<Step> steps) : base()
		{
			Freeze(steps);
		}

		public Genome(IEnumerable<StepCount> steps) : this(steps.Steps())
		{
		}

		public Genome(string steps) : base()
		{
			Freeze(Steps.FromGenomeHash(steps));
		}

		string _hash;
		public override string Hash
			=> _hash ?? GetHash();

		string GetHash()
			=> _genes.ToStepCounts().ToGenomeHash();

		public new Genome Clone()
			=> new Genome(_genes);

		protected override object CloneInternal()
			=> Clone();

		Step[] _genes;
		public ReadOnlySpan<Step> Genes => _genes.AsSpan();

		public override int Length => _genes.Length;


		protected override void OnBeforeFreeze()
		{
			_hash = GetHash();
			base.OnBeforeFreeze();
		}

		public void Freeze(IEnumerable<Step> steps)
		{
			_genes = steps.ToArray();
			_remainingVariations = GetVariations();
			Freeze();
		}

		protected override Genome Reduction()
		{
			var reducedSteps = _genes.Reduce();
			return reducedSteps == null
				? null
				: new Genome(reducedSteps);
		}

		public IEnumerator<Step> GetEnumerator()
			=> _genes.AsEnumerable().GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> _genes.GetEnumerator();

		static readonly IEnumerable<Step> ForwardOne = Enumerable.Repeat(Step.Forward, 1);

		IEnumerator<Genome> GetVariations()
			=> Variations().Where(g => g.Hash != Hash).GetEnumerator();

		IEnumerable<Genome> Variations()
		{
			yield return new Genome(_genes.Reverse());

			var lenMinusOne = _genes.Length - 1;
			if (lenMinusOne > 1)
			{
				{
					IEnumerable<Step> removeTail = _genes.Take(lenMinusOne).ToList();
					yield return new Genome(removeTail);
					yield return new Genome(Enumerable.Repeat(_genes[lenMinusOne], 1).Concat(removeTail));
				}
				{
					IEnumerable<Step> removeHead = _genes.Skip(1).ToList();
					yield return new Genome(removeHead);
					yield return new Genome(removeHead.Concat(Enumerable.Repeat(_genes[0], 1)));
				}

			}

			// All forward movement lengths reduced by 1.
			yield return new Genome(_genes.ToStepCounts().Select(sc => sc.Step == Step.Forward && sc.Count > 1 ? sc-- : sc));


			yield return new Genome(ForwardOne.Concat(_genes));
			yield return new Genome(_genes.Concat(ForwardOne));

			// All forward movement lengths doubled...
			yield return new Genome(_genes.SelectMany(g => Enumerable.Repeat(g, g == Step.Forward ? 2 : 1)));

			// Pattern is doubled.
			yield return new Genome(Enumerable.Repeat(_genes, 2).SelectMany(s => s));

			// Pass reduced last to allow for any interesting varations to occur above first.
			var reduced = AsReduced();
			if (reduced != this) yield return reduced;

			var half = reduced.Length / 2;
			if (half > 2)
			{
				yield return new Genome(reduced.Take(half));
				yield return new Genome(reduced.Skip(half));

				var third = reduced.Length / 3;
				if (third > 2)
				{
					yield return new Genome(reduced.Take(third));
					yield return new Genome(reduced.Skip(third).Take(third));
					yield return new Genome(reduced.Skip(third));
					yield return new Genome(reduced.Take(2 * third));
					yield return new Genome(reduced.Skip(2 * third));
				}
			}



		}

		IEnumerator<Genome> _remainingVariations;

		public override IEnumerator<IGenome> RemainingVariations
			=> _remainingVariations ?? GetVariations();

	}
}
