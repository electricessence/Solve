using Open.Collections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Multiplexer;

[SuppressMessage("ReSharper", "IteratorNeverReturns")]
public sealed class SampleCache
{
	public sealed class Entry
	{
		public readonly IReadOnlyList<bool> ParamValues;
		public readonly Lazy<bool> Correct;

		public Entry(IReadOnlyList<bool> paramValues, Formula f)
		{
			ParamValues = paramValues;
			Correct = Lazy.Create(() => f(ParamValues));
		}
	}

	readonly Formula _actualFormula;
	readonly ConcurrentDictionary<long, LazyList<Entry>> _sampleCache;

	public SampleCache(Formula actualFormula)
	{
		_actualFormula = actualFormula;
		_sampleCache = new ConcurrentDictionary<long, LazyList<Entry>>();
	}

	public IEnumerable<Entry> Generate()
	{
		while (true)
			yield return new Entry(Samples()/*.Distinct()*/.Memoize(true), _actualFormula);
	}

	public LazyList<Entry> Get(long id)
		=> _sampleCache.GetOrAdd(id, key => Generate().Memoize(true));

	// Boolean domain (address + data lines): each parameter is simply a coin flip.
	// Uses the BCL's Random.Shared directly -- Open.RandomizationExtensions no longer
	// exposes a static "Extensions.Random" surface; that package's current API is a set
	// of extension methods over System.Random/collections (e.g. RandomSelectOne(), as
	// used by Solve/Genomics/IGenomeFactory.cs and Solve.Evaluation/EvalGenomeFactoryBase.cs),
	// not a random-number-generation facade, so plain System.Random is the correct fit here.
	static IEnumerable<bool> Samples()
	{
		while (true)
			yield return Random.Shared.Next(2) == 1;
	}
}
