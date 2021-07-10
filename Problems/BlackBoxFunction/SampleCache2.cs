using Open.RandomizationExtensions;
using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using Open.Collections;
using System.Collections.Concurrent;
using System.Linq;

namespace BlackBoxFunction
{
	public sealed class SampleCache2
	{
		public sealed class Entry : LazyList<(IReadOnlyList<double> input, double correct)>
		{
			public Entry(IEnumerable<LazyList<double>> paramValues, Formula f)
				:base(GetResults(paramValues,f), true)
			{
			}

			public static IEnumerable<(IReadOnlyList<double> input, double correct)>GetResults(
				IEnumerable<LazyList<double>> paramValues, Formula f)
			{
				foreach (var pv in paramValues)
					yield return (pv, f(pv));
			}
		}


		public readonly double Range;
		public readonly int SampleSize;
		readonly Formula _actualFormula;
		readonly ConcurrentDictionary<long, Entry> _sampleCache = new();

		public SampleCache2(Formula actualFormula, int sampleSize = 100, double range = 100)
		{
			if (range == 0) throw new ArgumentException("Cannot be zero.", nameof(range));
			if (sampleSize < 1) throw new ArgumentOutOfRangeException(nameof(sampleSize), sampleSize, "Must be at least 1.");
			Range = range;
			SampleSize = sampleSize;
			_actualFormula = actualFormula;
		}

		public ImmutableArray<double> GetRandomLinearInput()
		{
			var start = Range * Randomizer.Random.NextDouble();
			var end = Range * Randomizer.Random.NextDouble();
			var delta = end - start;
			var last = SampleSize - 1;

			var builder = ImmutableArray.CreateBuilder<double>(SampleSize);
			builder.Count = SampleSize;
			builder[0] = start;
			for(var i = 1; i< last; ++i)
				builder[i] = start + i * delta / SampleSize;
			builder[last] = end;
			return builder.MoveToImmutable();
		}

		public IEnumerable<ImmutableArray<double>> RandomSample()
		{
			while(true)	yield return GetRandomLinearInput();
		}

		LazyList<LazyList<double>> Partition(IEnumerable<ImmutableArray<double>> sample)
		{
			var samples = sample.Memoize(true);
			return Enumerable.Range(0, SampleSize).Select(i =>
				Enumerable.Range(0, int.MaxValue).Select(j => samples[j][i]).Memoize())
				.Memoize(true);
		}

		public Entry GenerateEntry() => new(Partition(RandomSample()), _actualFormula);

		public Entry Get(long id) => _sampleCache.GetOrAdd(id, key => GenerateEntry());
	}
}
