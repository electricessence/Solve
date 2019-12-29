﻿using Open.Disposable;
using System;
using System.Text;

namespace Solve
{
	public static class ObjectPool<T>
		where T : class, new()
	{
		public static readonly InterlockedArrayObjectPool<T> Instance
			= InterlockedArrayObjectPool.Create<T>();
	}

	public static class StringBuilderPool
	{
		public static readonly InterlockedArrayObjectPool<StringBuilder> Instance
			= new InterlockedArrayObjectPool<StringBuilder>(
				() => new StringBuilder(),
				sb =>
				{
					sb.Clear();
					if (sb.Capacity > 16) sb.Capacity = 16;
				},
				null);

		public static string Rent(Action<StringBuilder> action)
		{
			var sb = Instance.Take();
			action(sb);
			var result = sb.ToString();
			Instance.Give(sb);
			return result;
		}
	}
}