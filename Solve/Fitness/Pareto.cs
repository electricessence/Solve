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

		ArrayPool<(T Value, ImmutableArray<double> Score)> pool
			= ArrayPool<(T Value, ImmutableArray<double> Score)>.Shared;
		(T Value, ImmutableArray<double> Score)[] p
			= pool.Rent(d.Count);

		try
		{
			int count = d.Count;
			d.Values.CopyTo(p, 0);
			d.Clear();

			// Keep the non-dominated set: X survives unless some other element dominates it.
			var dominated = new bool[count];
			for (int i = 0; i < count; i++)
			{
				ReadOnlySpan<double> x = p[i].Score.AsSpan();
				for (int j = 0; j < count; j++)
				{
					if (j == i || dominated[j]) continue;
					if (Dominates(p[j].Score.AsSpan(), x))
					{
						dominated[i] = true;
						break;
					}
				}
			}

			int kept = 0;
			for (int i = 0; i < count; i++)
			{
				if (!dominated[i]) p[kept++] = p[i];
			}

			var segment = new ArraySegment<(T Value, ImmutableArray<double> Score)>(p, 0, kept);
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

	/// <summary>
	/// True if <paramref name="a"/> Pareto-dominates <paramref name="b"/> (maximization):
	/// at least as good in every dimension and strictly better in at least one.
	/// NaN ranks below any non-NaN value; NaN versus NaN is equal.
	/// </summary>
	private static bool Dominates(in ReadOnlySpan<double> a, in ReadOnlySpan<double> b)
	{
		int len = a.Length;
		Debug.Assert(len == b.Length);
		bool strict = false;
		for (int i = 0; i < len; i++)
		{
			ref readonly double av = ref a[i];
			ref readonly double bv = ref b[i];
			bool aNaN = double.IsNaN(av);
			bool bNaN = double.IsNaN(bv);
			if (aNaN && bNaN) continue;
			if (aNaN) return false;
			if (bNaN) { strict = true; continue; }
			if (av < bv) return false;
			if (av > bv) strict = true;
		}

		return strict;
	}
}
