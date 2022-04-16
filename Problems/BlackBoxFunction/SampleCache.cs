using Open.Collections;
using Open.RandomizationExtensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace BlackBoxFunction;

public sealed class SampleCache
{
	public sealed class Entry
	{
		/// <summary>
		/// An immutable set of expandable values.
		/// </summary>
		public readonly IReadOnlyList<double> ParamValues;

		/// <summary>
		/// The correct value given the parameters.
		/// </summary>
		public readonly Lazy<double> Correct;

		public Entry(IReadOnlyList<double> paramValues, Formula f)
		{
			ParamValues = paramValues;
			Correct = Lazy.Create(() => f(ParamValues));
		}
	}

	readonly Formula _actualFormula;
	readonly ConcurrentDictionary<long, LazyList<double>> _deltaCache;
	readonly ConcurrentDictionary<long, LazyList<Entry>> _sampleCache;

	public readonly double Range;

	public SampleCache(Formula actualFormula, double range = 100)
	{
		if (range == 0) throw new ArgumentException("Cannot be zero.", nameof(range));
		Range = range;
		_actualFormula = actualFormula;
		_deltaCache = new();
		_sampleCache = new();
	}

	public IEnumerable<Entry> Generate(long deltaId)
	{
		Entry previous = null;
		foreach (var d in Deltas())
		{
			previous = new Entry(Samples(GetDeltas(deltaId))/*.Distinct()*/.Memoize(true), _actualFormula);
			yield return previous;
		}
	}

	LazyList<double> GetDeltas(long id)
		=> _deltaCache.GetOrAdd(id, key => Deltas().Memoize(true));

	public LazyList<Entry> Get(long id)
		=> _sampleCache.GetOrAdd(id, key => Generate(key).Memoize(true));

	/// <summary>
	/// The amount of change a specific parameter should be changing for a sample.
	/// </summary>
	IEnumerable<double> Deltas()
	{
		while (true)
		{
			yield return (Randomizer.Random.NextDouble() - 0.5) * Range;
		}
	}

	/// <summary>
	/// The value of the params given the amount of change for each, augmenting by random offset.
	/// </summary>
	/// <param name="deltas">The change in values.</param>
	IEnumerable<double> Samples(LazyList<double> deltas)
	{
		var range = Range * Range;
		var halfRange = range / 2;
		var value = (range - halfRange) * Randomizer.Random.NextDouble();
		foreach (var delta in deltas)
		{
			yield return value;
			value += delta;
		}
	}

}
