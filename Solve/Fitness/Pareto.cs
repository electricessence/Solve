using Open.Collections;
using Open.Memory;
using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Solve;

public static class Pareto
{
	private static ArrayPoolSegment<(T Value, ImmutableArray<double> Score)> FilterInternal<T>(
		IEnumerable<T> source,
		IEqualityComparer<T> equalityComparer,
		Func<T, ImmutableArray<double>> scoreSelector)
		where T : notnull
	{
		ArgumentNullException.ThrowIfNull(source);

		var d = new Dictionary<T, (T Value, ImmutableArray<double> Score)>(equalityComparer);
		foreach ((T Value, ImmutableArray<double> Score) in source
			.Select(s => (Value: s, Score: scoreSelector(s)))
			.OrderBy(s => s.Score, CollectionComparer.Double.Descending)) // Enforce distinct by ordering.
		{
			if (!d.ContainsKey(Value))
				d.Add(Value, (Value, Score));
		}

		bool found;
		ArrayPool<(T Value, ImmutableArray<double> Score)> pool
			= ArrayPool<(T Value, ImmutableArray<double> Score)>.Shared;
		(T Value, ImmutableArray<double> Score)[] p
			= pool.Rent(d.Count);

		try
		{
			ReadOnlySpan<(T Value, ImmutableArray<double> Score)> pSpan
				= p.AsSpan();
			Dictionary<T, (T Value, ImmutableArray<double> Score)>.ValueCollection values
				= d.Values;

			do
			{
				found = false;
				values.CopyTo(p, 0); // p is the return
				pSpan = pSpan[..values.Count];
				foreach ((T value, ImmutableArray<double> score) in pSpan)
				{
					if (IsGreaterThanAll(score.AsSpan(), values))
					{
						found = true;
						d.Remove(value);
					}
				}
			}
			while (found);

			d.Clear();
			var segment = new ArraySegment<(T Value, ImmutableArray<double> Score)>(p, 0, pSpan.Length);
			return new ArrayPoolSegment<(T Value, ImmutableArray<double> Score)>(segment, pool);
		}
		catch
		{
			pool.Return(p);
			throw;
		}
	}

	public static ArrayPoolSegment<(T Value, ImmutableArray<double> Score)> Filter<T>(
		IEnumerable<T> source,
		IEqualityComparer<T> equalityComparer,
		Func<T, ImmutableArray<double>> scoreSelector)
		where T : notnull
		=> FilterInternal(source, equalityComparer, scoreSelector);

	//public static List<(T Value, ImmutableArray<double> Score)> Filter<T>(
	//	in ReadOnlySpan<T> source,
	//	IEqualityComparer<T> equalityComparer,
	//	Func<T, ImmutableArray<double>> scoreSelector)
	//	where T : notnull
	//	=> FilterInternal(source.ToArray(), equalityComparer, scoreSelector);

	private static bool IsGreaterThanAll<T>(in ReadOnlySpan<double> score, IEnumerable<(T Value, ImmutableArray<double> Score)> values)
	{
		int len = score.Length;
		foreach ((T _, ImmutableArray<double> Score) in values)
		{
			Debug.Assert(Score.Length == len);
			ReadOnlySpan<double> os = Score.AsSpan();
			for (int i = 0; i < len; i++)
			{
				ref readonly double s = ref score[i];
				ref readonly double osv = ref os[i];
				if (double.IsNaN(s) && double.IsNaN(osv)) continue;
				if (double.IsNaN(osv)) return true;
				if (double.IsNaN(s) || s <= osv) return false;
			}

			return true;
		}

		return false;
	}
}
