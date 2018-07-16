using Open.Memory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Solve
{
	public static class Pareto
	{
		static List<(T Value, ReadOnlyMemory<double> Score)> FilterInternal<T>(
			IEnumerable<T> source,
			IEqualityComparer<T> equalityComparer,
			Func<T, ReadOnlyMemory<double>> scoreSelector)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			var d = new Dictionary<T, (T Value, ReadOnlyMemory<double> Score)>(equalityComparer);
			foreach (var (Value, Score) in source
				.Select(s => (Value: s, Score: scoreSelector(s)))
				.OrderBy(s => s.Score, MemoryComparer.Double.Descending)) // Enforce distinct by ordering.
			{
				if (!d.ContainsKey(Value))
					d.Add(Value, (Value, Score));
			}

			bool found;
			List<(T Value, ReadOnlyMemory<double> Score)> p;

			do
			{
				found = false;
				var values = d.Values;
				p = values.ToList();  // p is the return
				foreach (var (Value, Score) in p)
				{
					if (IsGreaterThanAll(Score.Span, values))
					{
						found = true;
						d.Remove(Value);
					}
				}
			}
			while (found);

			return p;
		}

		public static List<(T Value, ReadOnlyMemory<double> Score)> Filter<T>(
			IEnumerable<T> source,
			IEqualityComparer<T> equalityComparer,
			Func<T, ReadOnlyMemory<double>> scoreSelector)
			=> FilterInternal(source, equalityComparer, scoreSelector);

		public static List<(T Value, ReadOnlyMemory<double> Score)> Filter<T>(
			in ReadOnlySpan<T> source,
			IEqualityComparer<T> equalityComparer,
			Func<T, ReadOnlyMemory<double>> scoreSelector)
			=> FilterInternal(source.ToArray(), equalityComparer, scoreSelector);

		static bool IsGreaterThanAll<T>(in ReadOnlySpan<double> score, IEnumerable<(T Value, ReadOnlyMemory<double> Score)> values)
		{
			var len = score.Length;
			foreach (var (_, Score) in values)
			{
				Debug.Assert(Score.Length == len);
				var os = Score.Span;
				for (var i = 0; i < len; i++)
				{
					ref readonly var s = ref score[i];
					ref readonly var osv = ref os[i];
					if (double.IsNaN(s) && double.IsNaN(osv)) continue;
					if (double.IsNaN(osv)) return true;
					if (double.IsNaN(s) || s <= osv) return false;
				}
				return true;
			}
			return false;
		}
	}
}
