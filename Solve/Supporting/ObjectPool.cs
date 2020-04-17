using Open.Disposable;
using System;
using System.Collections.Generic;
using System.Text;

namespace Solve
{
	public static class ObjectPool<T>
		where T : class, new()
	{
		public static readonly OptimisticArrayObjectPool<T> Instance
			= OptimisticArrayObjectPool.Create<T>();
	}

	public static class ObjectPool
	{
		public static void Give<T>(T e)
			where T : class, new()
			=> ObjectPool<T>.Instance.Give(e);
	}

	public static class StringBuilderPool
	{
		public static readonly ConcurrentQueueObjectPool<StringBuilder> Instance
			= new ConcurrentQueueObjectPool<StringBuilder>(
				() => new StringBuilder(),
				sb =>
				{
					sb.Clear();
					if (sb.Capacity > 16) sb.Capacity = 16;
				},
				null,
				1024);

		public static string Rent(Action<StringBuilder> action)
		{
			var sb = Instance.Take();
			action(sb);
			var result = sb.ToString();
			Instance.Give(sb);
			return result;
		}


		public static StringBuilder Take()
			=> Instance.Take();

		public static void Give(StringBuilder sb)
			=> Instance.Give(sb);
	}

	public static class CollectionPool<T, TColl>
		where TColl : class, ICollection<T>, new()
	{
		public static readonly ConcurrentQueueObjectPool<TColl> Instance
			= new ConcurrentQueueObjectPool<TColl>(
				() => new TColl(),
				d => d.Clear(),
				null,
				1024);

		public static void Rent(Action<TColl> action)
		{
			var d = Instance.Take();
			action(d);
			Instance.Give(d);
		}

		public static TResult Rent<TResult>(Func<TColl, TResult> action)
		{
			var d = Instance.Take();
			var result = action(d);
			Instance.Give(d);
			return result;
		}
	}

	public static class DictionaryPool<TKey, TValue>
		where TKey : notnull
	{
		public static readonly ConcurrentQueueObjectPool<Dictionary<TKey, TValue>> Instance
			= new ConcurrentQueueObjectPool<Dictionary<TKey, TValue>>(
				() => new Dictionary<TKey, TValue>(),
				d => d.Clear(),
				null,
				1024);

		public static Dictionary<TKey, TValue> Take() => Instance.Take();

		public static void Rent(Action<Dictionary<TKey, TValue>> action)
		{
			var d = Instance.Take();
			action(d);
			Instance.Give(d);
		}

		public static TResult Rent<TResult>(Func<Dictionary<TKey, TValue>, TResult> action)
		{
			var d = Instance.Take();
			var result = action(d);
			Instance.Give(d);
			return result;
		}
	}

	public static class DictionaryPool
	{
		public static void Give<TKey, TValue>(Dictionary<TKey, TValue> dictionary)
			where TKey : notnull
			=> DictionaryPool<TKey, TValue>.Instance.Give(dictionary);
	}
}
