﻿using Open.Cloneable;
using Open.Collections;
using Solve;
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
			Genes = EMPTY;
		}

		public EaterGenome(IEnumerable<Step> steps) : base()
		{
			Freeze(steps);
		}

		public EaterGenome(string steps) : base()
		{
			Freeze(Steps.FromGenomeHash(steps));
		}

		string _hash;
		public override string Hash
			=> _hash ?? Genes.ToGenomeHash();

		public new EaterGenome Clone()
			=> new EaterGenome(Genes);

		protected override object CloneInternal()
			=> Clone();

		public IReadOnlyList<Step> Genes { get; private set; }

		public Step[] GetGenes()
			=> Genes.ToArray();

		protected override void OnBeforeFreeze()
		{
			_hash = Genes.ToGenomeHash();
			base.OnBeforeFreeze();
		}

		public void Freeze(IEnumerable<Step> steps)
		{
			Genes = steps.ToList().AsReadOnly();
			_remainingVariations = GetVariations();
			Freeze();
		}

		protected override EaterGenome Reduction()
		{
			var reducedSteps = Genes.Reduce();
			return reducedSteps == null
				? null
				: new EaterGenome(reducedSteps);
		}

		public IEnumerator<Step> GetEnumerator()
			=> Genes.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> Genes.GetEnumerator();

		static readonly IEnumerable<Step> ForwardOne = Enumerable.Repeat(Step.Forward, 1);

		IEnumerator<EaterGenome> GetVariations()
		{
			var lenMinusOne = Genes.Count - 1;
			if (lenMinusOne > 1)
			{
				{
					var removeTail = Genes.Take(lenMinusOne).ToList();
					yield return new EaterGenome(removeTail);
					yield return new EaterGenome(Enumerable.Repeat(Genes[lenMinusOne], 1).Concat(removeTail));
				}
				{
					var removeHead = Genes.Skip(1).ToList();
					yield return new EaterGenome(removeHead);
					yield return new EaterGenome(removeHead.Concat(Enumerable.Repeat(Genes[0], 1)));
				}

			}

			yield return new EaterGenome(ForwardOne.Concat(Genes));
			yield return new EaterGenome(Genes.Concat(ForwardOne));

			var doubled = new List<Step>();
			foreach (var s in Genes)
			{
				doubled.Add(s);
				if (s == Step.Forward)
					doubled.Add(s);
			}
			yield return new EaterGenome(doubled);
		}

		IEnumerator<EaterGenome> _remainingVariations;

		public override IEnumerator<IGenome> RemainingVariations
			=> _remainingVariations ?? GetVariations();

	}
}
