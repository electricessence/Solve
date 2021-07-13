using Open.Disposable;
using System;
using System.Collections.Generic;
using System.Text;

namespace Solve
{
	public static class StringBuilderPool
	{
		public static readonly ConcurrentQueueObjectPool<StringBuilder> Instance
			= new(
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

}
