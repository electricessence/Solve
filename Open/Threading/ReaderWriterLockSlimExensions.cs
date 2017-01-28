﻿/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/Open/blob/dotnet-core/LICENSE.md
 */

using System;
using System.Threading;


namespace Open.Threading
{

	public abstract class Lock : IDisposable
	{
		protected ReaderWriterLockSlim _target;
		public bool LockHeld;

		protected Lock(ReaderWriterLockSlim target, bool lockHeld)
		{
			_target = target;
			LockHeld = lockHeld;
		}
		protected abstract void OnDispose(ReaderWriterLockSlim target);

		public void Dispose()
		{
			var target = Interlocked.Exchange(ref _target, null);
			if (target != null) OnDispose(target);
		}
	}

	public sealed class ReadLock : Lock
	{
		public ReadLock(ReaderWriterLockSlim target, int? millisecondsTimeout = null)
		: base(target, target.EnterReadLock(millisecondsTimeout, true))
		{
		}

		protected override void OnDispose(ReaderWriterLockSlim target)
		{
			target.ExitReadLock();
		}
	}

	public sealed class WriteLock : Lock
	{
		public WriteLock(ReaderWriterLockSlim target, int? millisecondsTimeout = null)
		: base(target, target.EnterWriteLock(millisecondsTimeout, true))
		{
		}

		protected override void OnDispose(ReaderWriterLockSlim target)
		{
			target.ExitWriteLock();
		}
	}

	public sealed class UpgradableReadLock : Lock
	{
		readonly object _sync = new Object();
		WriteLock _upgraded;
		public UpgradableReadLock(ReaderWriterLockSlim target, int? millisecondsTimeout = null)
		: base(target, target.EnterUpgradeableReadLock(millisecondsTimeout, true))
		{
		}

		// A useful utility but it's completely fine to create your own write lock under an upgradable one.
		public void UpgradeToWriteLock(int? millisecondsTimeout = null)
		{
			lock (_sync)
			{
				if (_upgraded != null)
					throw new InvalidOperationException("A write lock is already in effect.");
				_upgraded = new WriteLock(_target, millisecondsTimeout);
			}
		}

		public void Downgrade()
		{
			lock (_sync)
			{
				if (_upgraded == null)
					throw new InvalidOperationException("There is no write lock in effect to downgrade from.");
				_upgraded.Dispose();
				_upgraded = null;
			}
		}

		protected override void OnDispose(ReaderWriterLockSlim target)
		{
			lock (_sync) Interlocked.Exchange(ref _upgraded, null).SmartDispose();
			target.ExitUpgradeableReadLock();
		}
	}

	public static class ReaderWriterLockSlimExensions
	{

		public static bool IsLockFree(this ReaderWriterLockSlim target)
		{
			if (target == null)
				throw new NullReferenceException();

			return target.CurrentReadCount == 0
				&& target.RecursiveReadCount == 0
				&& target.RecursiveUpgradeCount == 0
				&& target.RecursiveWriteCount == 0
				&& target.WaitingReadCount == 0
				&& target.WaitingWriteCount == 0
				&& target.WaitingUpgradeCount == 0
				&& !target.IsReadLockHeld
				&& !target.IsUpgradeableReadLockHeld
				&& !target.IsWriteLockHeld;
		}

		public static bool IsvalidMillisecondsTimeout(int? millisecondsTimeout)
		{
			return millisecondsTimeout == null || millisecondsTimeout >= 0;
		}

		internal static void ValidateMillisecondsTimeout(int? millisecondsTimeout)
		{
			if (millisecondsTimeout != null && millisecondsTimeout < 0)
				throw new ArgumentOutOfRangeException("millisecondsTimeout", millisecondsTimeout, "Cannot be a negative value.");
		}


		/// <summary>
		/// Simplifies entering a timeout or no timeout based read lock.
		/// </summary>
		/// <returns>True if a lock was acquired, false if not.</returns>
		public static bool EnterReadLock(this ReaderWriterLockSlim target,
			int? millisecondsTimeout, bool throwsOnTimeout = false)
		{
			if (target == null)
				throw new NullReferenceException();
			ValidateMillisecondsTimeout(millisecondsTimeout);

			if (millisecondsTimeout == null)
				target.EnterReadLock();
			else if (!target.TryEnterReadLock(millisecondsTimeout.Value))
			{
				if (throwsOnTimeout)
					throw new TimeoutException(
						"Could not gain a read lock within the timeout specified. " +
						"(millisecondsTimeout=" + millisecondsTimeout.Value + ") ");

				return false;
			}

			return true;
		}

		/// <summary>
		/// Simplifies entering a timeout or no timeout based upgradeable read lock.
		/// </summary>
		/// <returns>True if a lock was acquired, false if not.</returns>
		public static bool EnterUpgradeableReadLock(this ReaderWriterLockSlim target,
			int? millisecondsTimeout, bool throwsOnTimeout = false)
		{
			if (target == null)
				throw new NullReferenceException();
			ValidateMillisecondsTimeout(millisecondsTimeout);

			if (millisecondsTimeout == null)
				target.EnterUpgradeableReadLock();
			else if (!target.TryEnterUpgradeableReadLock(millisecondsTimeout.Value))
			{
				if (throwsOnTimeout)
					throw new TimeoutException(
						"Could not gain an upgradeable read lock within the timeout specified. " +
						"(millisecondsTimeout=" + millisecondsTimeout.Value + ") ");

				return false;
			}

			return true;
		}

		/// <summary>
		/// Simplifies entering a timeout or no timeout based write lock.
		/// </summary>
		/// <returns>True if a lock was acquired, false if not.</returns>
		public static bool EnterWriteLock(this ReaderWriterLockSlim target,
			int? millisecondsTimeout, bool throwsOnTimeout = false)
		{
			if (target == null)
				throw new NullReferenceException();
			ValidateMillisecondsTimeout(millisecondsTimeout);

			if (millisecondsTimeout == null)
				target.EnterWriteLock();
			else if (!target.TryEnterWriteLock(millisecondsTimeout.Value))
			{
				if (throwsOnTimeout)
					throw new TimeoutException(
						"Could not gain a write lock within the timeout specified. " +
						"(millisecondsTimeout=" + millisecondsTimeout.Value + ") ");

				return false;
			}

			return true;
		}

		/// <summary>
		/// ReaderWriterLockSlim extension for synchronizing read access.
		/// </summary>
		/// <param name="target">ReaderWriterLockSlim to execute on.</param>
		/// <param name="query">Action to execute once a lock is acquired.</param>
		/// <param name="millisecondsTimeout">Indicates if and for how long a timeout is used to acquire a lock.</param>
		/// <param name="throwsOnTimeout">If this parameter is true, then if a timeout addValue is reached, an exception is thrown.</param>
		/// <returns>Returns false if a timeout is reached.</returns>
		public static bool Read(this ReaderWriterLockSlim target,
			Action closure,
			int? millisecondsTimeout = null, bool throwsOnTimeout = false)
		{
			if (target == null)
				throw new NullReferenceException();
			if (closure == null)
				throw new ArgumentNullException("closure");
			ValidateMillisecondsTimeout(millisecondsTimeout);

			bool lockHeld = false;
			try
			{
				lockHeld = target.EnterReadLock(millisecondsTimeout, throwsOnTimeout);
				closure();
			}
			finally
			{
				if (lockHeld)
					target.ExitReadLock();
			}

			return lockHeld;
		}

		public static ReadLock ReadLock(this ReaderWriterLockSlim target,
			int? millisecondsTimeout = null)
		{
			return new ReadLock(target, millisecondsTimeout);
		}

		public static WriteLock WriteLock(this ReaderWriterLockSlim target,
			int? millisecondsTimeout = null)
		{
			return new WriteLock(target, millisecondsTimeout);
		}

		public static UpgradableReadLock UpgradableReadLock(this ReaderWriterLockSlim target,
			int? millisecondsTimeout = null)
		{
			return new UpgradableReadLock(target, millisecondsTimeout);
		}

		/// <summary>
		/// ReaderWriterLockSlim extension for synchronizing read access.
		/// </summary>
		/// <param name="target">ReaderWriterLockSlim to execute on.</param>
		/// <param name="query">Action to execute once a lock is acquired.</param>
		/// <param name="millisecondsTimeout">Indicates if and for how long a timeout is used to acquire a lock.</param>
		/// <param name="throwsOnTimeout">If this parameter is true, then if a timeout addValue is reached, an exception is thrown.</param>
		/// <returns>Returns false if a timeout is reached.</returns>
		public static bool Read<T>(this ReaderWriterLockSlim target,
			out T result, Func<T> closure,
			int? millisecondsTimeout = null, bool throwsOnTimeout = false)
		{
			if (target == null)
				throw new NullReferenceException();
			if (closure == null)
				throw new ArgumentNullException("closure");
			ValidateMillisecondsTimeout(millisecondsTimeout);

			bool lockHeld = false;
			try
			{
				lockHeld = target.EnterReadLock(millisecondsTimeout, throwsOnTimeout);
				result = closure();
			}
			finally
			{
				if (lockHeld)
					target.ExitReadLock();
			}

			return lockHeld;
		}


		/// <summary>
		/// ReaderWriterLockSlim extension for synchronizing upgradeable read access.
		/// This method allows for entering a write lock within the query, but there can only be one upgraded thread at at time.
		/// </summary>
		/// <param name="target">ReaderWriterLockSlim to execute on.</param>
		/// <param name="query">Action to execute once a lock is acquired.</param>
		/// <param name="millisecondsTimeout">Indicates if and for how long a timeout is used to acquire a lock.</param>
		/// <param name="throwsOnTimeout">If this parameter is true, then if a timeout addValue is reached, an exception is thrown.</param>
		/// <returns>Returns false if a timeout is reached.</returns>
		public static bool ReadUpgradeable(this ReaderWriterLockSlim target,
			Action closure,
			int? millisecondsTimeout = null, bool throwsOnTimeout = false)
		{
			if (target == null)
				throw new NullReferenceException();
			if (closure == null)
				throw new ArgumentNullException("closure");
			ValidateMillisecondsTimeout(millisecondsTimeout);

			bool lockHeld = false;
			try
			{
				lockHeld = target.EnterUpgradeableReadLock(millisecondsTimeout, throwsOnTimeout);
				closure();
			}
			finally
			{
				if (lockHeld)
					target.ExitUpgradeableReadLock();
			}

			return lockHeld;
		}

		/// <summary>
		/// ReaderWriterLockSlim extension for synchronizing upgradeable read access.
		/// This method allows for entering a write lock within the query, but there can only be one upgraded thread at at time.
		/// </summary>
		/// <param name="target">ReaderWriterLockSlim to execute on.</param>
		/// <param name="query">Action to execute once a lock is acquired.</param>
		/// <param name="millisecondsTimeout">Indicates if and for how long a timeout is used to acquire a lock.</param>
		/// <param name="throwsOnTimeout">If this parameter is true, then if a timeout addValue is reached, an exception is thrown.</param>
		/// <returns>Returns false if a timeout is reached.</returns>
		public static bool ReadUpgradeable<T>(this ReaderWriterLockSlim target,
			out T result, Func<T> closure,
			int? millisecondsTimeout = null, bool throwsOnTimeout = false)
		{
			if (target == null)
				throw new NullReferenceException();
			if (closure == null)
				throw new ArgumentNullException("closure");
			ValidateMillisecondsTimeout(millisecondsTimeout);

			bool lockHeld = false;
			try
			{
				lockHeld = target.EnterUpgradeableReadLock(millisecondsTimeout, throwsOnTimeout);
				result = closure();
			}
			finally
			{
				if (lockHeld)
					target.ExitUpgradeableReadLock();
			}

			return lockHeld;
		}


		/// <summary>
		/// ReaderWriterLockSlim extension for synchronizing write access.
		/// </summary>
		/// <param name="target">ReaderWriterLockSlim to execute on.</param>
		/// <param name="query">Action to execute once a lock is acquired.</param>
		/// <param name="millisecondsTimeout">Indicates if and for how long a timeout is used to acquire a lock.</param>
		/// <param name="throwsOnTimeout">If this parameter is true, then if a timeout addValue is reached, an exception is thrown.</param>
		/// <returns>Returns false if a timeout is reached.</returns>
		public static bool Write(this ReaderWriterLockSlim target,
			Action closure,
			int? millisecondsTimeout = null, bool throwsOnTimeout = false)
		{
			if (target == null)
				throw new NullReferenceException();
			if (closure == null)
				throw new ArgumentNullException("closure");
			ValidateMillisecondsTimeout(millisecondsTimeout);

			bool lockHeld = false;
			try
			{
				lockHeld = target.EnterWriteLock(millisecondsTimeout, throwsOnTimeout);
				closure();
			}
			finally
			{
				if (lockHeld)
					target.ExitWriteLock();
			}

			return lockHeld;
		}

		/// <summary>
		/// ReaderWriterLockSlim extension for synchronizing write access.
		/// </summary>
		/// <param name="target">ReaderWriterLockSlim to execute on.</param>
		/// <param name="query">Action to execute once a lock is acquired.</param>
		/// <param name="millisecondsTimeout">Indicates if and for how long a timeout is used to acquire a lock.</param>
		/// <param name="throwsOnTimeout">If this parameter is true, then if a timeout addValue is reached, an exception is thrown.</param>
		/// <returns>Returns false if a timeout is reached.</returns>
		public static bool Write<T>(this ReaderWriterLockSlim target,
			out T result, Func<T> closure,
			int? millisecondsTimeout = null, bool throwsOnTimeout = false)
		{
			if (target == null)
				throw new NullReferenceException();
			if (closure == null)
				throw new ArgumentNullException("closure");
			ValidateMillisecondsTimeout(millisecondsTimeout);

			bool lockHeld = false;
			try
			{
				lockHeld = target.EnterWriteLock(millisecondsTimeout, throwsOnTimeout);
				result = closure();
			}
			finally
			{
				if (lockHeld)
					target.ExitWriteLock();
			}

			return lockHeld;
		}

		/// <summary>
		/// ReaderWriterLockSlim extension for synchronizing write access.  Starts by executing the condition without a lock.
		/// Note: Passing a boolean to the condition when a lock is acquired helps if it is important to the cosuming logic to avoid recursive locking.
		/// </summary>
		/// <param name="condition">Takes a bool where false means no lock and true means a write lock.  Returns true if it should execute the query Action.</param>
		/// <returns>Returns false if a timeout is reached.</returns>
		public static bool WriteConditional(this ReaderWriterLockSlim target,
			Func<bool, bool> condition, Action closure,
			int? millisecondsTimeout = null, bool throwsOnTimeout = false)
		{
			if (target == null)
				throw new NullReferenceException();
			if (condition == null)
				throw new ArgumentNullException("condition");
			if (closure == null)
				throw new ArgumentNullException("closure");
			ValidateMillisecondsTimeout(millisecondsTimeout);

			var lockHeld = true;
			if (condition(false))
			{
				lockHeld = target.Write(() =>
				{
					if (condition(true))
						closure();
				},
				millisecondsTimeout,
				throwsOnTimeout);
			}

			return lockHeld;
		}

		/// <summary>
		/// ReaderWriterLockSlim extension for synchronizing write access.  Starts by executing the condition without a lock.
		/// Note: Passing a boolean to the condition when a lock is acquired helps if it is important to the cosuming logic to avoid recursive locking.
		/// </summary>
		/// <param name="condition">Takes a bool where false means no lock and true means a write lock.  Returns true if it should execute the query Action.</param>
		/// <returns>Returns false if a timeout is reached.</returns>
		public static bool WriteConditional<T>(this ReaderWriterLockSlim target,
			ref T result, Func<bool, bool> condition, Func<T> closure,
			int? millisecondsTimeout = null, bool throwsOnTimeout = false)
		{
			if (target == null)
				throw new NullReferenceException();
			if (condition == null)
				throw new ArgumentNullException("condition");
			if (closure == null)
				throw new ArgumentNullException("closure");
			ValidateMillisecondsTimeout(millisecondsTimeout);

			var r = result;
			bool lockHeld = true, written = false;
			if (condition(false))
			{
				lockHeld = target.Write(out written, () =>
				{
					var w = condition(true);
					if (w) r = closure();
					return w;
				},
				millisecondsTimeout,
				throwsOnTimeout);
			}
			if (written)
				result = r;

			return lockHeld;
		}

		/// <summary>
		/// ReaderWriterLockSlim extension for synchronizing write access.  Starts by executing the condition with a read lock.  Then if necessary after releasing the read lock, acquires a write lock.
		/// Note: Passing a boolean to the condition when a lock is acquired helps if it is important to the cosuming logic to avoid recursive locking.
		/// </summary>
		/// <param name="condition">Takes a bool where false means a read lock and true means a write lock.  Returns true if it should execute the query Action.</param>
		/// <returns>Returns false if a timeout is reached.</returns>
		public static bool ReadWriteConditional(this ReaderWriterLockSlim target,
			Func<LockType, bool> condition, Action closure,
			int? millisecondsTimeout = null, bool throwsOnTimeout = false)
		{
			if (target == null)
				throw new NullReferenceException();
			if (condition == null)
				throw new ArgumentNullException("condition");
			if (closure == null)
				throw new ArgumentNullException("closure");
			ValidateMillisecondsTimeout(millisecondsTimeout);

			bool c = false;
			var lockHeld = target.Read(() => c = condition(LockType.Read), millisecondsTimeout, throwsOnTimeout);
			if (lockHeld && c)
			{
				lockHeld = target.Write(() =>
				{
					if (condition(LockType.Write))
						closure();
				},
				millisecondsTimeout,
				throwsOnTimeout);

			}

			return lockHeld;
		}

		/// <summary>
		/// ReaderWriterLockSlim extension for synchronizing write access.  Starts by executing the condition with a read lock.  Then if necessary after releasing the read lock, acquires a write lock.
		/// Note: Passing a boolean to the condition when a lock is acquired helps if it is important to the cosuming logic to avoid recursive locking.
		/// </summary>
		/// <param name="condition">Takes a bool where false means a read lock and true means a write lock.  Returns true if it should execute the query Action.</param>
		/// <returns>Returns false if a timeout is reached.</returns>
		public static bool ReadWriteConditional<T>(this ReaderWriterLockSlim target,
			ref T result, Func<LockType, bool> condition, Func<T> closure,
			int? millisecondsTimeout = null, bool throwsOnTimeout = false)
		{
			if (target == null)
				throw new NullReferenceException();
			if (condition == null)
				throw new ArgumentNullException("condition");
			if (closure == null)
				throw new ArgumentNullException("closure");
			ValidateMillisecondsTimeout(millisecondsTimeout);

			var r = result;
			bool c = false, written = false;
			var lockHeld = target.Read(() => c = condition(LockType.Read), millisecondsTimeout, throwsOnTimeout);
			if (lockHeld && c)
			{
				lockHeld = target.Write(out written, () =>
				{
					var w = condition(LockType.Write);
					if (w) r = closure();
					return w;
				},
				millisecondsTimeout,
				throwsOnTimeout);

			}
			if (written)
				result = r;


			return lockHeld;
		}

		/// <summary>
		/// ReaderWriterLockSlim extension for synchronizing write access.  Starts by executing the condition with an upgradeable read lock before acquiring a write lock.
		/// Note: Passing a boolean to the condition when a lock is acquired helps if it is important to the cosuming logic to avoid recursive locking.
		/// </summary>
		/// <param name="condition">Takes a bool where false means a read lock and true means a write lock.  Returns true if it should execute the query Action.</param>
		/// <returns>Returns false if a timeout is reached.</returns>
		public static bool ReadUpgradeableWriteConditional(this ReaderWriterLockSlim target,
			Func<bool> condition, Action closure,
			int? millisecondsTimeout = null, bool throwsOnTimeout = false)
		{
			if (target == null)
				throw new NullReferenceException();
			if (condition == null)
				throw new ArgumentNullException("condition");
			if (closure == null)
				throw new ArgumentNullException("closure");
			ValidateMillisecondsTimeout(millisecondsTimeout);

			bool writeLocked = true; // Initialize true so that if only only reading it still returns true.
			bool readLocked = target.ReadUpgradeable(() =>
			{
				if (condition())
					writeLocked = target.Write(closure, millisecondsTimeout, throwsOnTimeout);
			});

			return readLocked && writeLocked;
		}

		/// <summary>
		/// ReaderWriterLockSlim extension for synchronizing write access.  Starts by executing the condition with an upgradeable read lock before acquiring a write lock.
		/// Note: Passing a boolean to the condition when a lock is acquired helps if it is important to the cosuming logic to avoid recursive locking.
		/// </summary>
		/// <param name="condition">Takes a bool where false means a read lock and true means a write lock.  Returns true if it should execute the query Action.</param>
		/// <returns>Returns false if a timeout is reached.</returns>
		public static bool ReadUpgradeableWriteConditional<T>(this ReaderWriterLockSlim target,
			ref T result, Func<bool> condition, Func<T> closure,
			int? millisecondsTimeout = null, bool throwsOnTimeout = false)
		{
			if (target == null)
				throw new NullReferenceException();
			if (condition == null)
				throw new ArgumentNullException("condition");
			if (closure == null)
				throw new ArgumentNullException("closure");
			ValidateMillisecondsTimeout(millisecondsTimeout);

			var r = result;
			bool writeLocked = true; // Initialize true so that if only only reading it still returns true.
			bool written = false;
			bool readLocked = target.ReadUpgradeable(() =>
			{
				if (condition())
				{
					// out r ensures that it IS written to.
					writeLocked = target.Write(out r, closure, millisecondsTimeout, throwsOnTimeout);
					written = true;
				}
			});

			if (written)
				result = r;


			return readLocked && writeLocked;
		}

		/// <summary>
		/// ReaderWriterLockSlim extension for synchronizing write access.  Starts by executing the condition with a read lock.
		/// Then if necessary executes the condition with an upgradeable read lock before acquiring a write lock.
		/// Note: Passing a boolean to the condition when a lock is acquired helps if it is important to the cosuming logic to avoid recursive locking.
		/// </summary>
		/// <param name="condition">Takes a bool where false means a read lock and true means a write lock.  Returns true if it should execute the query Action.</param>
		/// <returns>Returns false if a timeout is reached.</returns>
		public static bool ReadWriteConditionalOptimized(this ReaderWriterLockSlim target,
			Func<LockType, bool> condition, Action closure,
			int? millisecondsTimeout = null, bool throwsOnTimeout = false)
		{
			if (target == null)
				throw new NullReferenceException();
			if (condition == null)
				throw new ArgumentNullException("condition");
			if (closure == null)
				throw new ArgumentNullException("closure");
			ValidateMillisecondsTimeout(millisecondsTimeout);

			bool c = false;
			var lockHeld = target.Read(() => c = condition(LockType.Read), millisecondsTimeout, throwsOnTimeout);
			return lockHeld && (!c || target.ReadUpgradeableWriteConditional(() => condition(LockType.ReadUpgradeable), closure, millisecondsTimeout, throwsOnTimeout));
		}

		/// <summary>
		/// ReaderWriterLockSlim extension for synchronizing write access.  Starts by executing the condition with a read lock.
		/// Then if necessary executes the condition with an upgradeable read lock before acquiring a write lock.
		/// Note: Passing a boolean to the condition when a lock is acquired helps if it is important to the cosuming logic to avoid recursive locking.
		/// </summary>
		/// <param name="condition">Takes a bool where false means a read lock and true means a write lock.  Returns true if it should execute the query Action.</param>
		/// <returns>Returns false if a timeout is reached.</returns>
		public static bool ReadWriteConditionalOptimized<T>(this ReaderWriterLockSlim target,
			ref T result, Func<LockType, bool> condition, Func<T> closure,
			int? millisecondsTimeout = null, bool throwsOnTimeout = false)
		{
			if (target == null)
				throw new NullReferenceException();
			if (condition == null)
				throw new ArgumentNullException("condition");
			if (closure == null)
				throw new ArgumentNullException("closure");
			ValidateMillisecondsTimeout(millisecondsTimeout);

			bool c = false;
			var lockHeld = target.Read(() => c = condition(LockType.Read), millisecondsTimeout, throwsOnTimeout);
			return lockHeld && (!c || target.ReadUpgradeableWriteConditional(ref result, () => condition(LockType.ReadUpgradeable), closure, millisecondsTimeout, throwsOnTimeout));
		}

		/// <summary>
		/// If the getValue delegate returns null, the value is acquired from the createValue delegate.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="target"></param>
		/// <param name="getValue"></param>
		/// <param name="createValue"></param>
		/// <param name="millisecondsTimeout"></param>
		/// <param name="throwsOnTimeout"></param>
		/// <returns></returns>
		public static T GetOrCreateValue<T>(this ReaderWriterLockSlim target,
			Func<T> getValue, Func<T> createValue,
			int? millisecondsTimeout = null, bool throwsOnTimeout = false)
			where T : class
		{
			if (target == null)
				throw new NullReferenceException();
			if (getValue == null)
				throw new ArgumentNullException("getValue");
			if (createValue == null)
				throw new ArgumentNullException("createValue");
			ValidateMillisecondsTimeout(millisecondsTimeout);

			T result = null;
			target.ReadWriteConditionalOptimized(
				ref result, lockType => (result = getValue()) == null, createValue,
				millisecondsTimeout,
				throwsOnTimeout);

			return result;
		}

		/// <summary>
		/// ReaderWriterLockSlim extension for synchronizing read access.
		/// </summary>
		/// <returns>Returns false if a timeout is reached.</returns>
		public static bool TryReadValue<T>(this ReaderWriterLockSlim target,
			out T result, Func<T> valueFactory,
			int? millisecondsTimeout = null)
		{
			if (target == null)
				throw new NullReferenceException();
			if (valueFactory == null)
				throw new ArgumentNullException("valueFactory");
			ValidateMillisecondsTimeout(millisecondsTimeout);

			T r = default(T);
			var lockHeld = target.Read(() => r = valueFactory(), millisecondsTimeout, false);
			result = r;
			return lockHeld;
		}

		/// <summary>
		/// ReaderWriterLockSlim extension for synchronizing read access.
		/// </summary>
		/// <returns>Returns false if a timeout is reached.</returns>
		public static bool TryWriteValue<T>(this ReaderWriterLockSlim target,
			out T result, Func<T> valueFactory,
			int? millisecondsTimeout = null)
		{
			if (target == null)
				throw new NullReferenceException();
			if (valueFactory == null)
				throw new ArgumentNullException("valueFactory");
			ValidateMillisecondsTimeout(millisecondsTimeout);

			T r = default(T);
			var lockHeld = target.Write(() => r = valueFactory(), millisecondsTimeout, false);
			result = r;
			return lockHeld;
		}

		/// <summary>
		/// ReaderWriterLockSlim extension for synchronizing read access.
		/// </summary>
		public static T ReadValue<T>(this ReaderWriterLockSlim target,
			Func<T> valueFactory,
			int? millisecondsTimeout = null)
		{
			if (target == null)
				throw new NullReferenceException();
			if (valueFactory == null)
				throw new ArgumentNullException("valueFactory");
			ValidateMillisecondsTimeout(millisecondsTimeout);

			T result = default(T);
			target.Read(() => result = valueFactory(), millisecondsTimeout, true);
			return result;
		}

		/// <summary>
		/// ReaderWriterLockSlim extension for synchronizing write access.
		/// </summary>
		public static T WriteValue<T>(this ReaderWriterLockSlim target,
			Func<T> valueFactory,
			int? millisecondsTimeout = null)
		{
			if (target == null)
				throw new NullReferenceException();
			if (valueFactory == null)
				throw new ArgumentNullException("valueFactory");
			ValidateMillisecondsTimeout(millisecondsTimeout);

			T result = default(T);
			target.Write(() => result = valueFactory(), millisecondsTimeout, true);
			return result;
		}


	}
}
