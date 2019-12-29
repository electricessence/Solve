using Open.Cloneable;
using Solve;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Eater
{
	[DebuggerDisplay("{Genes.Length}:{_hash.Value}")]
	public sealed class Genome
		: GenomeBase, ICloneable<Genome>, IEnumerable<Step>
	{
		public Genome(ImmutableArray<Step> steps)
		{
			Freeze(steps);
		}

		public Genome(IEnumerable<Step> steps)
		{
			Freeze(steps);
		}

		public Genome(IEnumerable<StepCount> steps) : this(steps.Steps())
		{
		}

		public Genome(string steps) : this(Steps.FromGenomeHash(steps))
		{
		}

		protected override string GetHash()
			=> Genes.ToStepCounts().ToGenomeHash();

		public new Genome Clone()
			=> new Genome(Genes);

		protected override object CloneInternal()
			=> Clone();

		public ImmutableArray<Step> Genes { get; private set; }

		protected override int GetGeneCount() => Genes.Length;

		void Freeze(IEnumerable<Step> steps)
		{
			Freeze(steps is ImmutableArray<Step> s ? s : steps.ToImmutableArray());
		}

		void Freeze(ImmutableArray<Step> steps)
		{
			Genes = steps;
			Freeze();
		}

		public IEnumerator<Step> GetEnumerator()
			=> Genes.AsEnumerable().GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> Genes.AsEnumerable().GetEnumerator();

		public static implicit operator Genome(string steps) => new Genome(steps);
		public static implicit operator Genome(Step[] steps) => new Genome(steps);
		public static implicit operator Genome(StepCount[] steps) => new Genome(steps);
		public static implicit operator ImmutableArray<Step>(Genome genome) => genome.Genes;
	}
}
