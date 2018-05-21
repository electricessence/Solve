using Open.Cloneable;
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

		public Step[] GetGenes() => Genes.ToArray();

		protected override void OnBeforeFreeze()
		{
			_hash = Genes.ToGenomeHash();
			base.OnBeforeFreeze();
		}

		public void Freeze(IEnumerable<Step> steps)
		{
			Genes = steps.ToList().AsReadOnly();
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

	}
}
