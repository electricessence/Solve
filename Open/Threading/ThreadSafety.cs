/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/Open/blob/dotnet-core/LICENSE.md
 */

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Open.Diagnostics;


namespace Open.Threading
{
	/// <summary>
	/// Library class for executing different thread safe synchronization techniques.
	/// </summary>
	public static class ThreadSafety
	{
		public static bool IsValidSyncObject(object syncObject)
		{

			if (syncObject == null)
				return false;

			if (syncObject is IEnumerable)
				return true;

			// Avoid the lock object being immutable...

			if (syncObject is String)
				return false;

			if (syncObject is ValueType)
				return false;

			return true;
		}

		internal static void ValidateSyncObject(object syncObject)
		{
			if (syncObject == null)
				throw new ArgumentNullException("syncObject");
			if (!IsValidSyncObject(syncObject))
				throw new ArgumentException("syncObject");
		}

		public static bool InterlockedExchangeIfLessThanComparison(ref int location, int comparison, int newValue)
		{
			int initialValue;
			do
			{
				initialValue = location;
				if (initialValue >= comparison) return false;
			}
			while (Interlocked.CompareExchange(ref location, newValue, initialValue) != initialValue);
			return true;
		}

		public static bool InterlockedIncrementIfLessThanComparison(ref int location, int comparison, out int value)
		{
			int initialValue;
			do
			{
				initialValue = location;
				value = initialValue + 1;
				if (initialValue >= comparison) return false;
			}
			while (Interlocked.CompareExchange(ref location, value, initialValue) != initialValue);
			return true;
		}

		/*public static T EnsureInitialized<T>(ref T reference, Func<T> valueFactory) where T : class {
			Contract.Ensures(Contract.Result<T>() != null);
			// This throws if the value factory returns null;
			T result = LazyInitializer.EnsureInitialized<T>(ref reference, valueFactory);
			Contract.Assume(result != null);
			return result;
		}*/

		/// <summary>
		/// Applies a lock on the syncObject before executing the provided Action.
		/// This is keepgoing of a sample method and is direclty equivalient to using the lock keyword and is meant to be the equivalent of the below method without a timeout...
		/// </summary>
		public static void Lock<TSync>(TSync syncObject, Action closure) where TSync : class
		{
			ValidateSyncObject(syncObject);
			if (closure == null)
				throw new ArgumentNullException("closure");

			lock (syncObject)
				closure();
		}

		/// <summary>
		/// Applies a lock on the syncObject before executing the provided Action.
		/// This is keepgoing of a sample method and is direclty equivalient to using the lock keyword and is meant to be the equivalent of the below method without a timeout...
		/// </summary>
		/// <returns>The action of the query.</returns>
		public static T Lock<TSync, T>(TSync syncObject, Func<T> closure) where TSync : class
		{
			ValidateSyncObject(syncObject);
			if (closure == null)
				throw new ArgumentNullException("closure");

			lock (syncObject)
				return closure();
		}


		/// <summary>
		/// Applies a lock on the syncObject before executing the provided Action with a timeout.
		/// Throws a TimeoutException if throwsOnTimeout is true (default) and a lock could not be aquired.
		/// </summary>
		/// <param name="syncObject">Object used for synchronization.</param>
		/// <param name="query">The query to execute once a lock is acquired.</param>
		/// <param name="millisecondsTimeout">Maximum time allowed to wait for a lock.</param>
		/// <param name="throwsOnTimeout">If true and a timeout is reached, then a TimeoutException is thrown.
		/// If false and a timeout is reached, then it this method returns false and allows the caller to handle the failed lock.</param>
		/// <returns>
		/// True if a lock was acquired and the Action executed.
		/// False if throwsOnTimeout is false and could not acquire a lock.
		/// </returns>
		public static bool Lock<TSync>(TSync syncObject, Action closure, int millisecondsTimeout, bool throwsOnTimeout = true) where TSync : class
		{
			ValidateSyncObject(syncObject);
			if (closure == null)
				throw new ArgumentNullException("closure");
			if (millisecondsTimeout < 0)
				throw new ArgumentOutOfRangeException("closure", millisecondsTimeout, "Cannot be a negative value.");

			bool lockTaken = false;
			try
			{
				Monitor.TryEnter(syncObject, millisecondsTimeout, ref lockTaken);
				if (!lockTaken)
				{
					if (throwsOnTimeout)
						throw new TimeoutException("Could not gain a lock within the timeout specified.");

					return false;
				}

				closure();
			}
			finally
			{
				if (lockTaken) Monitor.Exit(syncObject);
			}

			return true;
		}

		/// <summary>
		/// Sychronizes executing the Action only if the condition is true.
		/// </summary>
		/// 
		/// <param name="syncObject">Object used for synchronization.</param>
		/// <param name="condition">Logic function to execute DCL pattern.  Passes in a boolean that is true for when a lock is held.  The return value indicates if a lock is still needed and the query should be executed.
		/// Note: Passing a boolean to the condition when a lock is acquired helps if it is important to the cosuming logic to avoid recursive locking.</param>
		/// <param name="query">The query to execute once a lock is acquired.  Only executes if the condition returns true.</param>
		/// <returns>
		/// True if the Action executed.
		/// </returns>						
		public static bool LockConditional<TSync>(TSync syncObject, Func<bool, bool> condition, Action closure) where TSync : class
		{
			ValidateSyncObject(syncObject);
			if (condition == null)
				throw new ArgumentNullException("condition");
			if (closure == null)
				throw new ArgumentNullException("closure");

			if (condition(false))
				lock (syncObject)
					if (condition(true)) { closure(); return true; }

			return false;
		}

		/// <summary>
		/// Sychronizes executing the Action only if the condition is true.
		/// </summary>
		/// 
		/// <param name="syncObject">Object used for synchronization.</param>
		/// <param name="condition">Logic function to execute DCL pattern.  The return value indicates if a lock is still needed and the query should be executed.</param>
		/// <param name="query">The query to execute once a lock is acquired.  Only executes if the condition returns true.</param>
		/// <returns>
		/// True if the Action executed.
		/// </returns>		
		public static bool LockConditional<TSync>(TSync syncObject, Func<bool> condition, Action closure) where TSync : class
		{
			ValidateSyncObject(syncObject);
			if (condition == null)
				throw new ArgumentNullException("condition");
			if (closure == null)
				throw new ArgumentNullException("closure");

			if (condition())
				lock (syncObject)
					if (condition()) { closure(); return true; }

			return false;
		}

		/// <summary>
		/// Sychronizes executing the Action only if the condition is true and using a timeout.
		/// Throws a TimeoutException if throwsOnTimeout is true (default) and a lock was needed but could not be aquired.
		/// </summary>
		/// 
		/// <param name="syncObject">Object used for synchronization.</param>
		/// <param name="condition">Logic function to execute DCL pattern.  Passes in a boolean that is true for when a lock is held.  The return value indicates if a lock is still needed and the query should be executed.
		/// Note: Passing a boolean to the condition when a lock is acquired helps if it is important to the cosuming logic to avoid recursive locking.</param>
		/// <param name="query">The query to execute once a lock is acquired.  Only executes if the condition returns true.</param>
		/// <param name="millisecondsTimeout">Maximum time allowed to wait for a lock.</param>
		/// <param name="throwsOnTimeout">If true and a timeout is reached, then a TimeoutException is thrown.
		///
		/// <returns>
		/// True if a lock was acquired and the Action executed.
		/// False if throwsOnTimeout is false and could not acquire a lock.
		/// </returns>
		public static bool LockConditional<TSync>(TSync syncObject, Func<bool, bool> condition, Action closure, int millisecondsTimeout, bool throwsOnTimeout = true) where TSync : class
		{
			ValidateSyncObject(syncObject);
			if (condition == null)
				throw new ArgumentNullException("condition");
			if (closure == null)
				throw new ArgumentNullException("closure");
			ReaderWriterLockSlimExensions.ValidateMillisecondsTimeout(millisecondsTimeout);

			if (condition(false))
			{
				bool lockTaken = false;
				try
				{
					Monitor.TryEnter(syncObject, millisecondsTimeout, ref lockTaken);
					if (!lockTaken)
					{
						if (throwsOnTimeout)
							throw new TimeoutException("Could not gain a lock within the timeout specified.");

						return false;
					}

					if (condition(true))
						closure();
				}
				finally
				{
					if (lockTaken) Monitor.Exit(syncObject);
				}
			}

			return true;
		}

		/// <summary>
		/// Sychronizes executing the Action only if the condition is true and using a timeout.
		/// Throws a TimeoutException if throwsOnTimeout is true (default) and a lock was needed but could not be aquired.
		/// </summary>
		/// 
		/// <param name="syncObject">Object used for synchronization.</param>
		/// <param name="condition">Logic function to execute DCL pattern.  The return value indicates if a lock is still needed and the query should be executed.</param>
		/// <param name="query">The query to execute once a lock is acquired.  Only executes if the condition returns true.</param>
		/// <param name="millisecondsTimeout">Maximum time allowed to wait for a lock.</param>
		/// <param name="throwsOnTimeout">If true and a timeout is reached, then a TimeoutException is thrown.
		///
		/// <returns>
		/// True if a lock was acquired and the Action executed.
		/// False if throwsOnTimeout is false and could not acquire a lock.
		/// </returns>
		public static bool LockConditional<TSync>(TSync syncObject, Func<bool> condition, Action closure, int millisecondsTimeout, bool throwsOnTimeout = true) where TSync : class
		{
			ValidateSyncObject(syncObject);
			if (condition == null)
				throw new ArgumentNullException("condition");
			if (closure == null)
				throw new ArgumentNullException("closure");
			ReaderWriterLockSlimExensions.ValidateMillisecondsTimeout(millisecondsTimeout);

			return LockConditional(syncObject, (locked) => condition(), closure, millisecondsTimeout, throwsOnTimeout);
		}

		/// <summary>
		/// Uses the provided lock object to sychronize acquiring the target value.
		/// If the target value is not set it sets the target to the query response.
		/// LazyIntializer will also work but this does not have the constraints of LazyInitializer.
		/// </summary>
		public static T LockIfNull<TSync, T>(TSync syncObject, ref T target, Func<T> closure) where TSync : class
		{
			ValidateSyncObject(syncObject);
			if (closure == null)
				throw new ArgumentNullException("closure");

			if (target == null)
				lock (syncObject)
					if (target == null)
						target = closure();
			return target;
		}


		// This is used to ensure not only thread safety but to ensure only a single operation.
		public static T InitializeValue<T>(ref Lazy<T> lazy, Func<T> factory)
		{
			LazyInitializer.EnsureInitialized(ref lazy, () => new Lazy<T>(factory, LazyThreadSafetyMode.ExecutionAndPublication));
			return lazy.Value;
		}


		private static readonly ConditionalWeakTable<object, ReadWriteHelper<object>> _sychronizeReadWriteRegistry
			= new ConditionalWeakTable<object, ReadWriteHelper<object>>();



		private static ReadWriteHelper<object> GetReadWriteHelper(object key)
		{
			if (key == null)
				throw new ArgumentNullException("key");
			var result = _sychronizeReadWriteRegistry.GetOrCreateValue(key);
			if (result == null)
				throw new NullReferenceException();
			return result;
		}

		public static bool SynchronizeReadWrite<TSync>(
			TSync syncObject,
			object key, Func<LockType, bool> condition, Action closure,
			int? millisecondsTimeout = null, bool throwsOnTimeout = true) where TSync : class
		{
			ValidateSyncObject(syncObject);
			if (key == null)
				throw new ArgumentNullException("key");
			if (condition == null)
				throw new ArgumentNullException("condition");
			if (closure == null)
				throw new ArgumentNullException("closure");
			ReaderWriterLockSlimExensions.ValidateMillisecondsTimeout(millisecondsTimeout);

			return GetReadWriteHelper(syncObject)
					.ReadWriteConditionalOptimized(key,
						condition, closure, millisecondsTimeout, throwsOnTimeout);

		}

		public static bool SynchronizeReadWrite<TSync, T>(
			TSync syncObject,
			object key, ref T result, Func<LockType, bool> condition, Func<T> closure,
			int? millisecondsTimeout = null, bool throwsOnTimeout = true) where TSync : class
		{
			ValidateSyncObject(syncObject);
			if (key == null)
				throw new ArgumentNullException("key");
			if (condition == null)
				throw new ArgumentNullException("condition");
			if (closure == null)
				throw new ArgumentNullException("closure");
			ReaderWriterLockSlimExensions.ValidateMillisecondsTimeout(millisecondsTimeout);

			return GetReadWriteHelper(syncObject)
					.ReadWriteConditionalOptimized(key, ref result,
						condition, closure, millisecondsTimeout, throwsOnTimeout);

		}

		public static bool SynchronizeReadWriteKeyAndObject<TSync>(
			TSync syncObject,
			object key, Func<LockType, bool> condition, Action closure,
			int? millisecondsTimeout = null, bool throwsOnTimeout = true) where TSync : class
		{
			ValidateSyncObject(syncObject);
			if (key == null)
				throw new ArgumentNullException("key");
			if (condition == null)
				throw new ArgumentNullException("condition");
			if (closure == null)
				throw new ArgumentNullException("closure");
			ReaderWriterLockSlimExensions.ValidateMillisecondsTimeout(millisecondsTimeout);

			return SynchronizeReadWrite(syncObject, key,
				lockType => SynchronizeRead(syncObject, () => condition(LockType.Read), millisecondsTimeout, throwsOnTimeout),
				() => SynchronizeReadWrite(syncObject, condition, closure, millisecondsTimeout, throwsOnTimeout),
				millisecondsTimeout,
				throwsOnTimeout);
		}

		public static bool SynchronizeReadWriteKeyAndObject<TSync, T>(
			TSync syncObject,
			object key, ref T result, Func<LockType, bool> condition, Func<T> closure,
			int? millisecondsTimeout = null, bool throwsOnTimeout = true) where TSync : class
		{
			ValidateSyncObject(syncObject);
			if (key == null)
				throw new ArgumentNullException("key");
			if (condition == null)
				throw new ArgumentNullException("condition");
			if (closure == null)
				throw new ArgumentNullException("closure");
			ReaderWriterLockSlimExensions.ValidateMillisecondsTimeout(millisecondsTimeout);


			var r = result;
			bool written = false;

			var synced = SynchronizeReadWriteKeyAndObject(
				syncObject, key, condition, () =>
				{
					r = closure();
					written = true;
				}, millisecondsTimeout, throwsOnTimeout);

			if (written)
				result = r;

			return synced;
		}

		public static bool SynchronizeReadWrite<TSync>(
			TSync syncObject,
			Func<LockType, bool> condition, Action closure,
			int? millisecondsTimeout = null, bool throwsOnTimeout = true) where TSync : class
		{
			ValidateSyncObject(syncObject);
			if (condition == null)
				throw new ArgumentNullException("condition");
			if (closure == null)
				throw new ArgumentNullException("closure");
			ReaderWriterLockSlimExensions.ValidateMillisecondsTimeout(millisecondsTimeout);

			return SynchronizeReadWrite(syncObject, syncObject, condition, closure, millisecondsTimeout, throwsOnTimeout);
		}

		public static bool SynchronizeReadWrite<TSync, T>(
			TSync syncObject,
			ref T result, Func<LockType, bool> condition, Func<T> closure,
			int? millisecondsTimeout = null, bool throwsOnTimeout = true) where TSync : class
		{
			ValidateSyncObject(syncObject);
			if (condition == null)
				throw new ArgumentNullException("condition");
			if (closure == null)
				throw new ArgumentNullException("closure");
			ReaderWriterLockSlimExensions.ValidateMillisecondsTimeout(millisecondsTimeout);

			return SynchronizeReadWrite(syncObject, syncObject, ref result, condition, closure, millisecondsTimeout, throwsOnTimeout);
		}

		//
		//	Summary:
		//		Manages a read-only operation of any target and specifc key of that object.
		//
		//	Parameters:
		//		syncObject:
		//			The main object that defines the synchronization context.
		//		key:
		//			The key that represents what value will change.
		//		closure:
		//			The function to execute while under a read lock.
		//		millisecondsTimeout:
		//			An optional value to allow for timeout.
		//
		//	Returns:
		//		The result of the closure.
		//	
		//	Exceptions:
		//		TimeoutException:
		//			Because we are returning a value then there must be a way to signal that a value in a read lock was not possible.  Only occurs if a millisecondsTimeout value is provided.		//
		//
		public static T SynchronizeRead<TSync, T>(TSync syncObject, object key, Func<T> closure, int? millisecondsTimeout = null) where TSync : class
		{
			ValidateSyncObject(syncObject);
			if (key == null)
				throw new ArgumentNullException("key");
			if (closure == null)
				throw new ArgumentNullException("closure");
			ReaderWriterLockSlimExensions.ValidateMillisecondsTimeout(millisecondsTimeout);

			return GetReadWriteHelper(syncObject)
				.ReadValue(key,
					closure, millisecondsTimeout);
		}

		//
		//	Summary:
		//		Manages a read-only operation of any target.
		//
		//	Parameters:
		//		syncObject:
		//			The main object that defines the synchronization context.
		//		closure:
		//			The function to execute while under a read lock.
		//		millisecondsTimeout:
		//			An optional value to allow for timeout.
		//
		//	Returns:
		//		The result of the closure.
		//	
		//	Exceptions:
		//		TimeoutException:
		//			Because we are returning a value then there must be a way to signal that a value in a read lock was not possible.  Only occurs if a millisecondsTimeout value is provided.
		//
		public static T SynchronizeRead<TSync, T>(TSync syncObject, Func<T> closure, int? millisecondsTimeout = null, bool throwsOnTimeout = true) where TSync : class
		{
			ValidateSyncObject(syncObject);
			if (closure == null)
				throw new ArgumentNullException("closure");
			ReaderWriterLockSlimExensions.ValidateMillisecondsTimeout(millisecondsTimeout);

			return SynchronizeRead(syncObject, syncObject, closure, millisecondsTimeout);
		}

		public static bool SynchronizeRead<TSync>(TSync syncObject, object key, Action closure, int? millisecondsTimeout = null, bool throwsOnTimeout = true) where TSync : class
		{
			ValidateSyncObject(syncObject);
			if (key == null)
				throw new ArgumentNullException("key");
			if (closure == null)
				throw new ArgumentNullException("closure");
			ReaderWriterLockSlimExensions.ValidateMillisecondsTimeout(millisecondsTimeout);


			return GetReadWriteHelper(syncObject)
				.Read(key,
					closure, millisecondsTimeout, throwsOnTimeout);
		}

		public static bool SynchronizeRead<TSync>(TSync syncObject, Action closure, int? millisecondsTimeout = null, bool throwsOnTimeout = true) where TSync : class
		{
			ValidateSyncObject(syncObject);
			if (closure == null)
				throw new ArgumentNullException("closure");
			ReaderWriterLockSlimExensions.ValidateMillisecondsTimeout(millisecondsTimeout);

			return SynchronizeRead(syncObject, syncObject, closure, millisecondsTimeout, throwsOnTimeout);
		}

		public static bool SynchronizeWrite<TSync>(TSync syncObject, object key, Action closure, int? millisecondsTimeout = null, bool throwsOnTimeout = true) where TSync : class
		{
			ValidateSyncObject(syncObject);
			if (key == null)
				throw new ArgumentNullException("key");
			if (closure == null)
				throw new ArgumentNullException("closure");
			ReaderWriterLockSlimExensions.ValidateMillisecondsTimeout(millisecondsTimeout);

			return GetReadWriteHelper(syncObject)
				.Write(key,
					closure, millisecondsTimeout, throwsOnTimeout);
		}

		public static bool SynchronizeWrite<TSync>(TSync syncObject, Action closure, int? millisecondsTimeout = null, bool throwsOnTimeout = true) where TSync : class
		{
			ValidateSyncObject(syncObject);
			if (closure == null)
				throw new ArgumentNullException("closure");
			ReaderWriterLockSlimExensions.ValidateMillisecondsTimeout(millisecondsTimeout);

			return SynchronizeWrite(syncObject, syncObject, closure, millisecondsTimeout, throwsOnTimeout);
		}

		public static void Execute(this Semaphore target, Action closure)
		{
			if (target == null)
				throw new ArgumentNullException("target");
			if (closure == null)
				throw new ArgumentNullException("closure");

			try
			{
				target.WaitOne();
				closure();
			}
			finally
			{
				try
				{
					target.Release();
				}
				catch (SemaphoreFullException sfex)
				{
					sfex.WriteToDebug();
				}
			}
		}


		public static void Execute(this SemaphoreSlim target, Action closure)
		{
			if (target == null)
				throw new ArgumentNullException("target");
			if (closure == null)
				throw new ArgumentNullException("closure");

			try
			{
				target.Wait();
				closure();
			}
			finally
			{
				try
				{
					target.Release();
				}
				catch (SemaphoreFullException sfex)
				{
					sfex.WriteToDebug();
				}
			}
		}

		public static T Execute<T>(this Semaphore target, Func<T> closure)
		{
			if (target == null)
				throw new ArgumentNullException("target");
			if (closure == null)
				throw new ArgumentNullException("closure");

			try
			{
				target.WaitOne();
				return closure();
			}
			finally
			{
				try
				{
					target.Release();
				}
				catch (SemaphoreFullException sfex)
				{
					sfex.WriteToDebug();
				}
			}
		}

		public static T Execute<T>(this SemaphoreSlim target, Func<T> closure)
		{
			if (target == null)
				throw new ArgumentNullException("target");
			if (closure == null)
				throw new ArgumentNullException("closure");

			try
			{
				target.Wait();
				return closure();
			}
			finally
			{
				try
				{
					target.Release();
				}
				catch (SemaphoreFullException sfex)
				{
					sfex.WriteToDebug();
				}
			}
		}


		public static async Task<T> ExecuteAsync<T>(this SemaphoreSlim target, Func<T> closure)
		{
			if (target == null)
				throw new ArgumentNullException("target");
			if (closure == null)
				throw new ArgumentNullException("closure");

			try
			{
				await target.WaitAsync().ConfigureAwait(false);
				return closure();
			}
			finally
			{
				try
				{
					target.Release();
				}
				catch (SemaphoreFullException sfex)
				{
					sfex.WriteToDebug();
				}
			}
		}

		public static async Task<T> ExecuteAsync<T>(this SemaphoreSlim target, Task<T> task)
		{
			if (target == null)
				throw new ArgumentNullException("target");
			if (task == null)
				throw new ArgumentNullException("task");

			try
			{
				await target.WaitAsync().ConfigureAwait(false);
				return await task;
			}
			finally
			{
				try
				{
					target.Release();
				}
				catch (SemaphoreFullException sfex)
				{
					sfex.WriteToDebug();
				}
			}
		}


		#region ReaderWriterLockSlim Extensions

		#endregion

		public class Helper<TKey, TSyncObject>
			where TSyncObject : class, new()
		{
			protected readonly ConcurrentDictionary<TKey, TSyncObject> _locks = new ConcurrentDictionary<TKey, TSyncObject>();

			public Helper()
			{
			}

			/// <summary>
			/// Returns a unique object based on the provied cacheKey for use in synchronization.
			/// </summary>
			public TSyncObject this[TKey key]
			{
				get
				{
					if (key == null)
						throw new ArgumentNullException("target");

					return _locks.GetOrAdd(key, k => new TSyncObject())
						?? new TSyncObject(); // Satisfies code contracts... (Will never actually occur).
				}
			}

			/// <summary>
			/// Clears all synchronization objects.
			/// </summary>
			public void Reset()
			{
				_locks.Clear();
			}





			/// <summary>
			/// Sychronizes executing the Action based on the cacheKey provided.
			/// </summary>
			public void Lock(TKey key, Action closure)
			{
				if (key == null)
					throw new ArgumentNullException("key");
				if (closure == null)
					throw new ArgumentNullException("closure");

				ThreadSafety.Lock(this[key], closure);
			}

			/// <summary>
			/// Sychronizes executing the Action based on the cacheKey provided using a timeout.
			/// Throws a TimeoutException if throwsOnTimeout is true (default) and a lock could not be aquired.
			/// </summary>
			public void Lock(TKey key, Action closure, int millisecondsTimeout, bool throwsOnTimeout = true)
			{
				ThreadSafety.Lock(this[key], closure, millisecondsTimeout, throwsOnTimeout);
			}







			/// <summary>
			/// Sychronizes executing the Action only if the condition is true based on the cacheKey provided.
			/// </summary>
			public void LockConditional(TKey key, Func<bool> condition, Action closure)
			{
				ThreadSafety.LockConditional(this[key], condition, closure);
			}

			/// <summary>
			/// Sychronizes executing the Action only if the condition is true based on the cacheKey provided using a timeout.
			/// Throws a TimeoutException if throwsOnTimeout is true (default) and a lock could not be aquired.
			/// </summary>
			public void LockConditional(TKey key, Func<bool> condition, Action closure, int millisecondsTimeout, bool throwsOnTimeout)
			{
				ThreadSafety.LockConditional(this[key], condition, closure, millisecondsTimeout, throwsOnTimeout);
			}


		}


		public class Helper<TKey> : Helper<TKey, object>
			where TKey : class
		{

			public Helper()
			{
			}

		}




		public class Helper : Helper<string>
		{

		}

		#region Nested type: File
		public static class File
		{

			internal static void ValidatePath(string path)
			{
				if (path == null)
					throw new ArgumentNullException("path");
				if (String.IsNullOrWhiteSpace(path))
					throw new ArgumentException("Cannot be empty or white space.", "path");
			}

			static ReadWriteHelper<string> _instance;
			private static ReadWriteHelper<string> Instance
			{
				get
				{
					return LazyInitializer.EnsureInitialized(ref _instance, () => new ReadWriteHelper<string>())
						?? new ReadWriteHelper<string>(); // Code contract resolution...
				}
			}

			/// <summary>
			/// Manages registering a ReaderWriterLockSlim an synchronizing the provided query write access.
			/// </summary>
			public static bool WriteTo(string path, Action closure,
				int? millisecondsTimeout = null, bool throwsOnTimeout = false)
			{
				ValidatePath(path);

				return Instance.Write(path, closure, millisecondsTimeout, throwsOnTimeout);
			}

			/// <summary>
			/// Manages file stream write access and retries.
			/// </summary>
			private static void WriteToInternal(string path, Action<FileStream> closure,
				int retries = DEFAULT_RETRIES,
				int millisecondsRetryDelay = DEFAULT_RETRYDELAY,
				int? millisecondsTimeout = null,
				bool throwsOnTimeout = false,
				FileMode mode = FileMode.OpenOrCreate,
				FileAccess access = FileAccess.Write,
				FileShare share = FileShare.None)
			{
				if (closure == null)
					throw new ArgumentNullException("closure");
				WriteTo(path, () =>
				{
					using (FileStream fs = Unsafe.GetFileStream(path, retries, millisecondsRetryDelay, mode, access, share))
						closure(fs);
				},
				millisecondsTimeout, throwsOnTimeout);
			}

			/// <summary>
			/// Manages file stream read access and retries.
			/// </summary>
			public static void WriteTo(string path, Action<FileStream> closure,
				int retries = DEFAULT_RETRIES,
				int millisecondsRetryDelay = DEFAULT_RETRYDELAY,
				int? millisecondsTimeout = null,
				bool throwsOnTimeout = false)
			{
				WriteToInternal(path, closure, retries, millisecondsRetryDelay, millisecondsTimeout, throwsOnTimeout, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
			}

			/// <summary>
			/// Manages file stream read access and retries.
			/// </summary>
			public static void AppendTo(string path, Action<FileStream> closure,
				int retries = DEFAULT_RETRIES,
				int millisecondsRetryDelay = DEFAULT_RETRYDELAY,
				int? millisecondsTimeout = null,
				bool throwsOnTimeout = false)
			{
				WriteToInternal(path, closure, retries, millisecondsRetryDelay, millisecondsTimeout, throwsOnTimeout, FileMode.Append, FileAccess.Write, FileShare.None);
			}

			/// <summary>
			/// Manages file stream read access and retries.
			/// </summary>
			public static void AppendLineTo(string path, string text,
				int retries = DEFAULT_RETRIES,
				int millisecondsRetryDelay = DEFAULT_RETRYDELAY,
				int? millisecondsTimeout = null,
				bool throwsOnTimeout = false)
			{
				if (text == null)
					throw new ArgumentNullException("text");

				ThreadSafety.File.AppendTo(path, fs =>
				{
					using (var sw = new StreamWriter(fs))
					{
						sw.WriteLine(text);
						sw.Flush();
					}
				}, retries, millisecondsRetryDelay, millisecondsTimeout, throwsOnTimeout);
			}

			/// <summary>
			/// Manages registering a ReaderWriterLockSlim an synchronizing the provided query write access.
			/// </summary>
			public static T WriteTo<T>(string path, Func<T> closure,
				int? millisecondsTimeout = null)
			{
				ValidatePath(path);

				return Instance.WriteValue(path, closure, millisecondsTimeout);
			}

			/// <summary>
			/// Manages registering a ReaderWriterLockSlim an synchronizing the provided query read access.
			/// </summary>
			public static bool ReadFrom(string path, Action closure,
				int? millisecondsTimeout = null, bool throwsOnTimeout = false)
			{
				ValidatePath(path);

				return Instance.Read(path, closure, millisecondsTimeout, throwsOnTimeout);
			}

			/// <summary>
			/// Manages registering a ReaderWriterLockSlim an synchronizing the provided query read access.
			/// </summary>
			public static bool ReadFromUpgradeable(
				string path, Action closure,
				int? millisecondsTimeout = null, bool throwsOnTimeout = false)
			{
				ValidatePath(path);

				return Instance.ReadUpgradeable(path, closure, millisecondsTimeout, throwsOnTimeout);
			}

			/// <summary>
			/// Manages registering a ReaderWriterLockSlim an synchronizing the provided query read access.
			/// </summary>
			public static bool ReadFromUpgradeable<T>(
				out T result, string path, Func<T> closure,
				int? millisecondsTimeout = null, bool throwsOnTimeout = false)
			{
				ValidatePath(path);

				return Instance.ReadUpgradeable(path, out result, closure, millisecondsTimeout, throwsOnTimeout);
			}

			public static bool WriteToIfNotExists(
				string path, Action closure,
				int? millisecondsTimeout = null, bool throwsOnTimeout = false)
			{

				bool writtenTo = false;
				if (!Exists(path)) // TODO: Implement out bool writtenTo
				{
					ReadFromUpgradeable(out writtenTo, path, () =>
					{
						if (!System.IO.File.Exists(path))
						{
							ThreadSafety.File.WriteTo(path, closure);
							return true;
						}
						return false;
					});
				}
				return writtenTo;
			}

			/// <summary>
			/// Manages registering a ReaderWriterLockSlim an synchronizing the provided query read access.
			/// </summary>
			public static T ReadFrom<T>(string path, Func<T> closure,
				int? millisecondsTimeout = null)
			{
				ValidatePath(path);

				return Instance.ReadValue(path, closure, millisecondsTimeout);
			}

			private const int DEFAULT_RETRIES = 4;
			private const int DEFAULT_RETRYDELAY = 4;


			/// <summary>
			/// Manages file stream read access and retries.
			/// </summary>
			public static void ReadFrom(string path, Action<FileStream> closure,
				int retries = DEFAULT_RETRIES,
				int millisecondsRetryDelay = DEFAULT_RETRYDELAY,
				int? millisecondsTimeout = null,
				bool throwsOnTimeout = false)
			{
				ValidatePath(path);
				if (closure == null)
					throw new ArgumentNullException("closure");

				ReadFrom(path, () =>
				{
					using (FileStream fs = Unsafe.GetFileStreamForRead(path, retries, millisecondsRetryDelay))
						closure(fs);
				},
				millisecondsTimeout, throwsOnTimeout);
			}


			/// <summary>
			/// Manages file stream read access and retries.
			/// </summary>
			public static T ReadFrom<T>(string path, Func<FileStream, T> closure,
				int retries = DEFAULT_RETRIES,
				int millisecondsRetryDelay = DEFAULT_RETRYDELAY,
				int? millisecondsTimeout = null)
			{
				if (closure == null)
					throw new ArgumentNullException("closure");

				return ReadFrom(path, () =>
				{
					using (FileStream fs = Unsafe.GetFileStreamForRead(path, retries, millisecondsRetryDelay))
						return closure(fs);
				}, millisecondsTimeout);
			}

			public static string ReadToString(string path, int retries = DEFAULT_RETRIES,
				int millisecondsRetryDelay = DEFAULT_RETRYDELAY,
				int? millisecondsTimeout = null)
			{
				return ReadFrom(path, (fs) =>
				{
					using (var reader = new StreamReader(fs))
						return reader.ReadToEnd();
				}, retries, millisecondsRetryDelay, millisecondsTimeout);
			}

			public static class Unsafe
			{
				// TODO: Add async await version...
				public static FileStream GetFileStream(string path, int retries, int millisecondsRetryDelay,
					FileMode mode, FileAccess access, FileShare share, int bufferSize = 4096)
				{
					ValidatePath(path);

					FileStream fs = null;
					int failCount = 0;
					do
					{
						// Need to retry in case of cross process locking...
						try
						{
							fs = new FileStream(path, mode, access, share, bufferSize, false);
							failCount = 0;
						}
						catch (IOException ioex)
						{
							failCount++;
							if (failCount > retries)
								throw;

							Debug.WriteLineIf(failCount == 1, "Error when acquring file stream: " + ioex.Message);
						}

						if (failCount != 0)
							Thread.Sleep(millisecondsRetryDelay);

					} while (failCount != 0);

					return fs;
				}

				public static async Task<FileStream> GetFileStreamAsync(string path, int retries, int millisecondsRetryDelay,
					FileMode mode, FileAccess access, FileShare share, int bufferSize = 4096)
				{
					ValidatePath(path);

					FileStream fs = null;
					int failCount = 0;
					do
					{
						// Need to retry in case of cross process locking...
						try
						{
							fs = new FileStream(path, mode, access, share, bufferSize, true);
							failCount = 0;
						}
						catch (IOException ioex)
						{
							failCount++;
							if (failCount > retries)
								throw;

							Debug.WriteLineIf(failCount == 1, "Error when acquring file stream: " + ioex.Message);
						}

						if (failCount != 0)
							await Task.Delay(millisecondsRetryDelay);

					} while (failCount != 0);

					return fs;
				}

				public static FileStream GetFileStreamForRead(
					string path,
					int retries = DEFAULT_RETRIES,
					int millisecondsRetryDelay = DEFAULT_RETRYDELAY,
					int bufferSize = 4096, bool useAsync = false)
				{
					return Unsafe.GetFileStream(path, retries, millisecondsRetryDelay,
						FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize);
				}

				public static Task<FileStream> GetFileStreamForReadAsync(
				string path,
				int retries = DEFAULT_RETRIES,
				int millisecondsRetryDelay = DEFAULT_RETRYDELAY,
				int bufferSize = 4096, bool useAsync = false)
				{
					return Unsafe.GetFileStreamAsync(path, retries, millisecondsRetryDelay,
						FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize);
				}

			}





			public static FileStream GetFileStreamForRead(string path,
				int retries = DEFAULT_RETRIES,
				int millisecondsRetryDelay = DEFAULT_RETRYDELAY,
				int? millisecondsTimeout = null)
			{

				return ReadFrom(path, () => Unsafe.GetFileStreamForRead(path, retries, millisecondsRetryDelay), millisecondsTimeout);
			}



			/// <summary>
			/// Uses registered read access conditions to determine if a file exists.
			/// </summary>
			public static bool Exists(string path,
				int? millisecondsTimeout = null)
			{
				ValidatePath(path);

				return ReadFrom(path, () => System.IO.File.Exists(path), millisecondsTimeout);
			}

			public static void EnsureDirectory(string path,
				int? millisecondsTimeout = null)
			{
				ValidatePath(path);

				path = Path.GetDirectoryName(path);

				if (!ReadFrom(path, () => Directory.Exists(path), millisecondsTimeout))
				{
					ThreadSafety.File.ReadFromUpgradeable(path, () =>
					{
						if (!Directory.Exists(path))
							ThreadSafety.File.WriteTo(path,
								() => Directory.CreateDirectory(path),
								millisecondsTimeout);
					});
				}
			}

			#endregion


		}
	}
}
