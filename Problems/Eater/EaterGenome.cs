using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Solve;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections;

namespace Eater
{

	public sealed class EaterGenome : ReducibleGenomeBase<EaterGenome>, ICloneable<EaterGenome>, IEnumerable<Step>
	{

		static readonly Step[] EMPTY = new Step[0];

		public EaterGenome() : base()
		{
			_steps = EMPTY;
		}

		public EaterGenome(IEnumerable<Step> steps) : base()
		{
			Freeze(steps);
		}

		public EaterGenome(string steps) : base()
		{
			Freeze(Steps.FromGenomeHash(steps));
		}


		Step[] _steps;

		string _hash;
		public override string Hash
		{
			get
			{
				return _hash ?? _steps.ToGenomeHash();
			}
		}

		public new EaterGenome Clone()
		{
			return new EaterGenome(this._steps);
		}

		protected override object CloneInternal()
		{
			return this.Clone();
		}

		Lazy<Step[]> _genes;
		public Step[] Genes
		{
			get
			{
				var g = _genes;
				return !IsReadOnly || g == null ? GetGenes() : g.Value;
			}
		}

		Step[] GetGenes()
		{
			return _steps.ToArray();
		}

		protected override void OnBeforeFreeze()
		{
			_hash = _steps.ToGenomeHash();
			base.OnBeforeFreeze();
		}

		public void Freeze(IEnumerable<Step> steps)
		{
			_steps = steps.ToArray();
			this.Freeze();
		}

		protected override EaterGenome Reduction()
		{
			var reducedSteps = _steps.Reduce();
			return reducedSteps == null ? null : new EaterGenome(reducedSteps);
		}

		public IEnumerator<Step> GetEnumerator()
		{
			return ((IEnumerable<Step>)_steps).GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<Step>)_steps).GetEnumerator();
		}
	}
}
