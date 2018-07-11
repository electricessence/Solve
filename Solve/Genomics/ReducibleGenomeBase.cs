using Open.Cloneable;
using System;
using System.Threading;

namespace Solve
{
	public abstract class ReducibleGenomeBase<TThis> : GenomeBase, IReducibleGenome<TThis>, ICloneable<TThis>
		where TThis : ReducibleGenomeBase<TThis>
	{
		public bool IsReducible => AsReduced() != this;

		TThis _reduced;
		public TThis AsReduced(bool ensureClone = false)
		{
			TThis reduced = IsFrozen
				? LazyInitializer.EnsureInitialized(ref _reduced, Reduction)
				: Reduction() ?? (TThis)this;

			return ensureClone ? reduced.Clone() : reduced;
		}

		public new TThis Clone() => (TThis)CloneInternal();

		protected abstract TThis Reduction();

		public void ReplaceReduced(TThis reduced)
		{
			if (reduced == null)
				throw new ArgumentNullException(nameof(reduced));
			if (_reduced != reduced)
			{
				if (IsFrozen && _reduced != null && _reduced.Hash != reduced.Hash)
					throw new InvalidOperationException("Attempting to replace a reduced genome with one that doesn't match.");
				_reduced = reduced;
			}
		}

		object IReducible.AsReduced(bool ensureClone) => AsReduced(ensureClone);
	}
}
