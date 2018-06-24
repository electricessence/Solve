using Open.Collections;
using Open.Numeric;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Eater
{
	public struct ReadOnlyXY<T>
		where T : struct
	{
		public ReadOnlyXY(in T x, in T y)
		{
			X = x;
			Y = y;
		}
		public readonly T X;
		public readonly T Y;

		public void Deconstruct(out T X, out T Y)
		{
			X = this.X;
			Y = this.Y;
		}
	}

	public struct Sample<T>
		where T : struct
	{
		public Sample(in ReadOnlyXY<T> start, in ReadOnlyXY<T> food)
		{
			Start = start;
			Food = food;
		}

		public readonly ReadOnlyXY<T> Start;
		public readonly ReadOnlyXY<T> Food;

		public void Deconstruct(out ReadOnlyXY<T> Start, out ReadOnlyXY<T> Food)
		{
			Start = this.Start;
			Food = this.Food;
		}
	}

	public sealed class SampleCache : IReadOnlyList<Sample<int>>
	{
		readonly LazyList<Sample<int>> _source;

		readonly ReadOnlyXY<int> _boundary;
		public ref readonly ReadOnlyXY<int> Boundary => ref _boundary;

		public int Count { get; }

		public Sample<int> this[in int index] => _source[index];
		Sample<int> IReadOnlyList<Sample<int>>.this[int index] => _source[index];

		public SampleCache(in ReadOnlyXY<int> boundary)
		{
			if (boundary.X < 2 || boundary.Y < 2)
				throw new ArgumentOutOfRangeException(nameof(boundary), boundary, "Must be at least 2 in width and height.");
			_boundary = boundary;

			var points = boundary.X & boundary.Y;
			var max = Math.Sqrt(int.MaxValue);
			if (points > max)
				throw new ArgumentOutOfRangeException(nameof(boundary), boundary, "Possibilties are too large to compute.");
			Count = points * points - points;
			_source = new LazyList<Sample<int>>(GenerateOrdered());
		}

		public SampleCache(in int size) : this(new ReadOnlyXY<int>(size, size))
		{

		}

		public IEnumerable<ReadOnlyXY<int>> GenerateXY()
		{
			for (var y = 0; y < Boundary.Y; y++)
			{
				for (var x = 0; x < Boundary.X; x++)
				{
					yield return new ReadOnlyXY<int>(x, y);
				}
			}
		}

		public IEnumerable<Sample<int>> GenerateOrdered()
		{
			foreach (var start in GenerateXY())
			{
				foreach (var food in GenerateXY())
				{
					if (!start.Equals(food))
						yield return new Sample<int>(start, food);
				}
			}
		}

		public IReadOnlyList<Sample<int>> Shuffled()
			=> new RandomizedSampleIndexTranslator(this);

		public static IReadOnlyList<Sample<int>> Shuffled(in ReadOnlyXY<int> boundary)
			=> new SampleCache(boundary).Shuffled();

		public static IReadOnlyList<Sample<int>> Shuffled(in int size)
			=> new SampleCache(size).Shuffled();

		IEnumerator<Sample<int>> IEnumerable<Sample<int>>.GetEnumerator()
			=> _source.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> _source.GetEnumerator();

		sealed class RandomizedSampleIndexTranslator : IReadOnlyList<Sample<int>>
		{
			static void Shuffle<T>(T[] list)
			{
				int n = list.Length;
				while (n > 1)
				{
					n--;
					int k = RandomUtilities.Random.Next(n + 1);
					T value = list[k];
					list[k] = list[n];
					list[n] = value;
				}
			}

			readonly int[] _indexes;
			readonly SampleCache _source;
			public RandomizedSampleIndexTranslator(SampleCache source)
			{
				_source = source ?? throw new ArgumentNullException(nameof(source));
				_indexes = Enumerable.Range(0, source.Count).ToArray();
				Shuffle(_indexes);
			}

			public int Count => _source.Count;
			public Sample<int> this[int index] => _source[in _indexes[index]];

			public IEnumerator<Sample<int>> GetEnumerator()
			{
				var len = _source.Count;
				for (var i = 0; i < len; i++)
					yield return this[i];
			}

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();
		}
	}

}
