using System;
using System.Diagnostics;
using System.Threading;

namespace Solve
{
	public abstract class ReducibleGenomeBase<T, TThis> : GenomeBase<T>, IReducibleGenome<TThis>, ICloneable<TThis>
		where TThis : ReducibleGenomeBase<T, TThis>
	{
		public bool IsReducible
		{
			get
			{
				return AsReduced() != this;
			}
		}

		protected TThis Reduced;
		public TThis AsReduced(bool ensureClone = false)
		{
			TThis reduced;
			if (!IsReadOnly)
			{
				reduced = Reduce();
				if (reduced == null) reduced = ensureClone ? this.Clone() : (TThis)this;
			}
			else
			{
				reduced = ensureClone ? Reduced.Clone() : Reduced;
			}
			return reduced;
		}

		public new TThis Clone()
		{
			return (TThis)CloneInternal();
		}

		protected abstract TThis Reduce();

		public void ReplaceReduced(TThis reduced)
		{
			if (reduced == null)
				throw new ArgumentNullException("reduced");
			if (Reduced != reduced)
			{
				if (IsReadOnly && Reduced != null && Reduced.Hash != reduced.Hash)
					throw new InvalidOperationException("Attempting to replace a reduced genome with one that doesn't match.");
				Reduced = reduced;
			}
		}

		protected override void OnBeforeFreeze()
		{
			if (Debugger.IsAttached)
			{
				// Validate that reduction isn't being trampled in weird ways.
				var reduced = Reduce() ?? (TThis)this;
				LazyInitializer.EnsureInitialized(ref Reduced, () => reduced);
				Debug.Assert(Reduced.Hash == reduced.Hash, "Existing reduction does not match actual reduction..");
			}
			else
			{
				LazyInitializer.EnsureInitialized(ref Reduced, () => Reduce() ?? (TThis)this);
			}
		}
	}
}
