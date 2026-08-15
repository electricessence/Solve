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

	/// <summary>
	/// Produces a stable (process-independent) 32-bit seed from a level id and a parameter
	/// dimension index. Deliberately avoids <see cref="HashCode.Combine{T1, T2}"/>, whose
	/// per-process randomized seed would make sampling non-reproducible across runs; this uses
	/// a fixed avalanche mix (the MurmurHash3 fmix64 finalizer) instead, so the same (id,
	/// dimension) pair always yields the same seed everywhere.
	/// </summary>
	static int MixSeed(long id, int dimension)
	{
		unchecked
		{
			var h = (ulong)id * 0x9E3779B97F4A7C15UL ^ (uint)dimension;
			h ^= h >> 33;
			h *= 0xff51afd7ed558ccdUL;
			h ^= h >> 33;
			h *= 0xc4ceb9fe1a85ec53UL;
			h ^= h >> 33;
			return (int)h;
		}
	}

	/// <summary>
	/// Generates the <see cref="SampleSize"/> values for a single input dimension of a level,
	/// as independent pseudo-random draws spanning [-<see cref="Range"/>, <see cref="Range"/>).
	/// Deterministic per (<paramref name="id"/>, <paramref name="dimension"/>): every sample is
	/// drawn independently, so samples within a level are not collinear in parameter space.
	/// </summary>
	public ImmutableArray<double> GetRandomVector(long id, int dimension)
	{
		var rng = new Random(MixSeed(id, dimension));

		var builder = ImmutableArray.CreateBuilder<double>(SampleSize);
		builder.Count = SampleSize;
		for (var i = 0; i < SampleSize; ++i)
			builder[i] = Range * (rng.NextDouble() * 2 - 1); // Uniform in [-Range, Range).
		return builder.MoveToImmutable();
	}

	public IEnumerable<ImmutableArray<double>> RandomSample(long id)
	{
		for (var dimension = 0; ; ++dimension)
			yield return GetRandomVector(id, dimension);
	}

	LazyList<LazyList<double>> Partition(IEnumerable<ImmutableArray<double>> sample)
	{
		var samples = sample.Memoize(true);
		return Enumerable.Range(0, SampleSize).Select(i =>
			Enumerable.Range(0, int.MaxValue).Select(j => samples[j][i]).Memoize())
			.Memoize(true);
	}

	public Entry GenerateEntry(long id) => new(Partition(RandomSample(id)), _actualFormula);

	public Entry Get(long id) => _sampleCache.GetOrAdd(id, GenerateEntry);
}
