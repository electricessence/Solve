using Open.Memory;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Solve;

public static class Pareto
{
	private static List<(T Value, ImmutableArray<double> Score)> FilterInternal<T>(
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
		List<(T Value, ImmutableArray<double> Score)> p;

		do
		{
			found = false;
			Dictionary<T, (T Value, ImmutableArray<double> Score)>.ValueCollection values = d.Values;
			p = values.ToList();  // p is the return
			foreach ((T Value, ImmutableArray<double> Score) in p)
			{
				if (IsGreaterThanAll(Score.AsSpan(), values))
				{
					found = true;
					d.Remove(Value);
				}
			}
		}
		while (found);

		d.Clear();
		return p;
	}

	public static List<(T Value, ImmutableArray<double> Score)> Filter<T>(
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
