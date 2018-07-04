using Open.Memory;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Solve
{
	public static partial class FitnessExtensions
	{

		public static List<KeyValuePair<TKey, double[]>> Pareto<TKey>(
			this IEnumerable<KeyValuePair<TKey, double[]>> population)
		{
			if (population == null)
				throw new ArgumentNullException(nameof(population));

			var d = new Dictionary<TKey, KeyValuePair<TKey, double[]>>();
			foreach (var entry in population.OrderBy(g => g.Value, ArrayComparer<double>.Descending)) // Enforce distinct by ordering.
			{
				var key = entry.Key;
				if (!d.ContainsKey(key)) d.Add(key, entry);
			}

			bool found;
			List<KeyValuePair<TKey, double[]>> p;

			do
			{
				found = false;
				var values = d.Values;
				p = values.ToList();  // p is the return
				foreach (var g in p)
				{
					var gs = g.Value;
					var len = gs.Length;
					if (values.Any(o =>
					{
						var os = o.Value;
						for (var i = 0; i < len; i++)
						{
							var osv = os[i];
							if (double.IsNaN(osv)) return true;
							if (gs[i] <= os[i]) return false;
						}
						return true;
					}))
					{
						found = true;
						d.Remove(g.Key);
					}
				}
			}
			while (found);

			return p;
		}

		public static List<KeyValuePair<TKey, double[]>> Pareto<TKey>(
			this IEnumerable<(TKey, double[])> population)
			=> Pareto(population.Select(p => new KeyValuePair<TKey, double[]>(p.Item1, p.Item2)));
	}
}
