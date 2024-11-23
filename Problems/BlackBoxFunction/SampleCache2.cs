using Open.Collections;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace BlackBoxFunction;

public sealed class SampleCache2
{
	public sealed class Entry(IEnumerable<LazyList<double>> paramValues, Formula f)
		: IReadOnlyList<(IReadOnlyList<double> input, double correct)>
	{
		public (IReadOnlyList<double> input, double correct) this[int index] => Values[index];

		public LazyList<(IReadOnlyList<double> input, double correct)> Values { get; } = new(GetResults(paramValues, f), true);

		public int Count => Values.Count;

		public static IEnumerable<(IReadOnlyList<double> input, double correct)> GetResults(
			IEnumerable<LazyList<double>> paramValues, Formula f)
		{
			foreach (var pv in paramValues)
				yield return (pv, f(pv));
		}

		public IEnumerator<(IReadOnlyList<double> input, double correct)> GetEnumerator() => Values.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Values).GetEnumerator();
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
		var start = Range * Random.Shared.NextDouble();
		var end = Range * Random.Shared.NextDouble();
		var delta = end - start;
		var last = SampleSize - 1;

		var builder = ImmutableArray.CreateBuilder<double>(SampleSize);
		builder.Count = SampleSize;
		builder[0] = start;
		for (var i = 1; i < last; ++i)
			builder[i] = start + i * delta / SampleSize;
		builder[last] = end;
		return builder.MoveToImmutable();
	}

	public IEnumerable<ImmutableArray<double>> RandomSample()
	{
		while (true) yield return GetRandomLinearInput();
	}

	LazyList<LazyList<double>> Partition(IEnumerable<ImmutableArray<double>> sample)
	{
		var samples = sample.Memoize(true);
		return Enumerable.Range(0, SampleSize).Select(i =>
			Enumerable.Range(0, int.MaxValue).Select(j => samples[j][i]).Memoize())
			.Memoize(true);
	}

	public Entry GenerateEntry() => new(Partition(RandomSample()), _actualFormula);

	public Entry Get(long id) => _sampleCache.GetOrAdd(id, _ => GenerateEntry());
}
