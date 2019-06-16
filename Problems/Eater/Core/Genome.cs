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
		: GenomeBase, ICloneable<Genome>, IEnumerable<Step>
	{
		static readonly Step[] EMPTY = Array.Empty<Step>();

		public Genome()
		{
			_genes = EMPTY;
		}

		public Genome(IEnumerable<Step> steps)
		{
			Freeze(steps);
		}

		public Genome(IEnumerable<StepCount> steps) : this(steps.Steps())
		{
		}

		public Genome(string steps)
		{
			Freeze(Steps.FromGenomeHash(steps));
		}

		protected override string GetHash()
			=> _genes.ToStepCounts().ToGenomeHash();

		public new Genome Clone()
			=> new Genome(_genes);

		protected override object CloneInternal()
			=> Clone();

		Step[] _genes;
		public ReadOnlySpan<Step> Genes => _genes.AsSpan();

		protected override int GetGeneCount() => _genes.Length;

		public void Freeze(IEnumerable<Step> steps)
		{
			_genes = steps.ToArray();
			Freeze();
		}

		public IEnumerator<Step> GetEnumerator()
			=> _genes.AsEnumerable().GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> _genes.GetEnumerator();

		public static implicit operator Genome(string steps) => new Genome(steps);
		public static implicit operator Genome(Step[] steps) => new Genome(steps);
		public static implicit operator Genome(StepCount[] steps) => new Genome(steps);
		public static implicit operator ReadOnlySpan<Step>(Genome genome) => genome.Genes;
	}
}
