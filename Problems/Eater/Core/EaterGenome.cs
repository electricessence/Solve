using Open.Cloneable;
using Solve;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Eater
{
	[DebuggerDisplay("{_hash}")]
	public sealed class EaterGenome
		: ReducibleGenomeBase<EaterGenome>, ICloneable<EaterGenome>, IEnumerable<Step>
	{

		static readonly Step[] EMPTY = System.Array.Empty<Step>();

		public EaterGenome() : base()
		{
			_genes = EMPTY;
		}

		public EaterGenome(IEnumerable<Step> steps) : base()
		{
			Freeze(steps);
		}

		public EaterGenome(IEnumerable<StepCount> steps) : this(steps.Steps())
		{
		}

		public EaterGenome(string steps) : base()
		{
			Freeze(Steps.FromGenomeHash(steps));
		}

		string _hash;
		public override string Hash
			=> _hash ?? GetHash();

		string GetHash()
			=> _genes.ToStepCounts().ToGenomeHash();

		public new EaterGenome Clone()
			=> new EaterGenome(_genes);

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

		protected override EaterGenome Reduction()
		{
			var reducedSteps = _genes.Reduce();
			return reducedSteps == null
				? null
				: new EaterGenome(reducedSteps);
		}

		public IEnumerator<Step> GetEnumerator()
			=> _genes.AsEnumerable().GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> _genes.GetEnumerator();

		static readonly IEnumerable<Step> ForwardOne = Enumerable.Repeat(Step.Forward, 1);

		IEnumerator<EaterGenome> GetVariations()
			=> Variations().Where(g => g.Hash != Hash).GetEnumerator();

		IEnumerable<EaterGenome> Variations()
		{
			yield return new EaterGenome(_genes.Reverse());

			var lenMinusOne = _genes.Length - 1;
			if (lenMinusOne > 1)
			{
				{
					var removeTail = _genes.Take(lenMinusOne).ToList();
					yield return new EaterGenome(removeTail);
					yield return new EaterGenome(Enumerable.Repeat(_genes[lenMinusOne], 1).Concat(removeTail));
				}
				{
					var removeHead = _genes.Skip(1).ToList();
					yield return new EaterGenome(removeHead);
					yield return new EaterGenome(removeHead.Concat(Enumerable.Repeat(_genes[0], 1)));
				}

			}

			// All forward movement lengths reduced by 1.
			yield return new EaterGenome(_genes.ToStepCounts().Select(sc => sc.Step == Step.Forward && sc.Count > 1 ? sc-- : sc));


			yield return new EaterGenome(ForwardOne.Concat(_genes));
			yield return new EaterGenome(_genes.Concat(ForwardOne));

			// All forward movement lengths doubled...
			yield return new EaterGenome(_genes.SelectMany(g => Enumerable.Repeat(g, g == Step.Forward ? 2 : 1)));

			// Pattern is doubled.
			yield return new EaterGenome(Enumerable.Repeat(_genes, 2).SelectMany(s => s));

			// Pass reduced last to allow for any interesting varations to occur above first.
			var reduced = AsReduced();
			if (reduced != this) yield return reduced;

			var half = reduced.Length / 2;
			if (half > 2)
			{
				yield return new EaterGenome(reduced.Take(half));
				yield return new EaterGenome(reduced.Skip(half));
			}

		}

		IEnumerator<EaterGenome> _remainingVariations;

		public override IEnumerator<IGenome> RemainingVariations
			=> _remainingVariations ?? GetVariations();

	}
}
