using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Open;
using Open.Collections;

namespace AlgebraBlackBox
{
	public sealed class SampleCache
	{
		public sealed class Entry
		{
			public readonly LazyList<double> ParamValues;
			public readonly Lazy<double> Correct;

			public Entry(LazyList<double> paramValues, Formula f)
			{
				ParamValues = paramValues;
				Correct = Lazy.New(() => f(ParamValues));
			}
		}

		readonly Formula _actualFormula;
		readonly ConcurrentDictionary<long, LazyList<Entry>> _sampleCache;

		public readonly double Range;

		public SampleCache(Formula actualFormula, double range = 100)
		{
			Range = range;
			_actualFormula = actualFormula;
			_sampleCache = new ConcurrentDictionary<long, LazyList<Entry>>();
		}

		public IEnumerable<Entry> Generate()
		{
			while (true) yield return new Entry(Samples().Distinct().Memoize(true), _actualFormula);
		}

		public LazyList<Entry> Get(long id)
		{
			return _sampleCache.GetOrAdd(id, key => Generate().Memoize(true));
		}

		IEnumerable<double> Samples()
		{
			var offset = RandomUtilities.Random.Next(1000) - Range / 2;
			while (true)
			{
				yield return RandomUtilities.Random.NextDouble() * Range + offset;
			}
		}

	}
}