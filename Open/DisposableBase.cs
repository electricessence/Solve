/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/Open/blob/dotnet-core/LICENSE.md
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;


namespace Open
{
	public sealed class DisposeHelper
	{
		// Since all write operations are done through Interlocked, no need for volatile.
		private int _disposeState;

		/// <summary>
		/// Gets a source indicating whether the container
		/// has not yet been disposed or is in the process of disposing.
		/// </summary>
		public bool IsLiving
		{
			get { return _disposeState == 0; }
		}

		/// <summary>
		/// Gets a source indicating whether the container has been disposed of.
		/// </summary>
		public bool IsDisposed
		{
			get { return _disposeState == 2; }
		}

		public event EventHandler BeforeDispose;

		// Dispose(bool calledExplicitly) executes in two distinct scenarios.
		// If calledExplicitly equals true, the method has been called directly
		// or indirectly by a user's code. Managed and unmanaged resources
		// can be disposed.
		// If calledExplicitly equals false, the method has been called by the
		// runtime from inside the finalizer and you should not reference
		// other objects. Only unmanaged resources can be disposed.
		public void Dispose(IDisposable target, Action<bool> OnDispose, bool calledExplicitly)
		{
			// Lock disposal...
			if (0 == Interlocked.CompareExchange(ref _disposeState, 1, 0))
			{
				// For calledExplicitly, throw on errors.
				// If by the GC (aka finalizer) don't throw,
				// since it's ignored anyway and creates overhead.

				// Fire events first because some internals may need access.
				try
				{
					if (BeforeDispose != null)
					{
						BeforeDispose(this, EventArgs.Empty);
						BeforeDispose = null;

						var db = target as DisposableBase;
						if (db != null)
							db.FireBeforeDispose();
					}
				}
				catch (Exception eventBeforeDisposeException)
				{
					if (!calledExplicitly)
					{
						if (Debugger.IsAttached)
							Debug.Fail(eventBeforeDisposeException.ToString());
					}
					else
						throw;
				}

				// Then do internal cleanup.
				try
				{
					if (OnDispose != null)
						OnDispose(calledExplicitly);
				}
				catch (Exception onDisposeException)
				{
					if (!calledExplicitly)
					{
						if (Debugger.IsAttached)
							Debug.Fail(onDisposeException.ToString());
					}
					else
						throw;
				}

				Interlocked.Exchange(ref _disposeState, 2);
			}

			// This object will be cleaned up by the Dispose method.
			// Therefore, you should call GC.SupressFinalize to
			// take this object off the finalization queue
			// and prevent finalization code for this object
			// from executing a second time.
			if (calledExplicitly)
				GC.SuppressFinalize(target);

		}

	}

	public abstract class DisposableBase : IDisposable
	{
		protected DisposeHelper DisposingHelper = new DisposeHelper();

		#region IDisposable Members
		/// <summary>
		/// Standard IDisposable 'Dispose' method.
		/// Triggers cleanup of this class and suppresses garbage collector usage.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
		}
		#endregion

		//private string _disposedFrom;

		protected void Dispose(bool calledExplicitly)
		{
			try
			{
				OnBeforeDispose();
			}
			finally
			{
				var dh = Interlocked.Exchange(ref DisposingHelper, null);
				if (dh != null)
				{
					dh.Dispose(this, OnDispose, calledExplicitly);
				}
			}
		}

		// Can occur multiple times.
		protected virtual void OnBeforeDispose() { }

		// Occurs only once.
		protected abstract void OnDispose(bool calledExplicitly);

		// Being called by the GC...
		~DisposableBase()
		{
			Dispose(false);
		}

		public event EventHandler BeforeDispose;
		internal void FireBeforeDispose()
		{
			if (BeforeDispose != null)
			{
				BeforeDispose(this, EventArgs.Empty);
				BeforeDispose = null;
			}
		}

		public bool IsDisposed
		{
			get
			{
				var dh = DisposingHelper;
				return dh == null || !dh.IsLiving;
			}
		}

		public bool AssertIsLiving()
		{
			//Contract.Ensures(!IsDisposed);
			if (IsDisposed)
				throw new ObjectDisposedException(this.ToString());

			return true;
		}


	}

	public static class DisposableExtensions
	{
		public static void AssertIsLiving(this DisposeHelper target)
		{
			if (target == null)
				throw new NullReferenceException();

			if (!target.IsLiving)
				throw new ObjectDisposedException(target.ToString());
		}

		public static void DisposeAll(this IEnumerable<IDisposable> target)
		{
			if (target == null)
				throw new ArgumentNullException("target");

			foreach (var d in target)
			{
				if (d != null)
					d.Dispose();
			}
		}

		public static void SmartDispose(this IDisposable target)
		{
			if (target != null)
				target.Dispose();
		}

		public static void SmartDispose<T>(this ICollection<T> target)
		{
			if (target != null)
			{
				target.Clear();
				if (target is IDisposable)
				{
					((IDisposable)target).Dispose();
				}
			}
		}
	}
}
