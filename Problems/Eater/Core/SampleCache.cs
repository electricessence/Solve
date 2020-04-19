using Open.Collections;
using Open.Numeric;
using Open.RandomizationExtensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Eater
{
	public sealed class SampleCache
	{
		public struct Entry
		{
			public readonly Point EaterStart;
			public readonly Point Food;

			public Entry(Point eaterStart, Point food)
			{
				EaterStart = eaterStart;
				Food = food;
			}
		}

		readonly ConcurrentDictionary<long, LazyList<Entry>> _sampleCache;

		public readonly ushort GridSize;

		public readonly Size Boundary;
		public readonly int SeedOffset;

		public SampleCache(ushort gridSize)
		{
			if (gridSize < 2)
				throw new ArgumentOutOfRangeException(nameof(gridSize), gridSize, "Must be at least 2.");
			GridSize = gridSize;
			Boundary = new Size(gridSize, gridSize);

			_sampleCache = new ConcurrentDictionary<long, LazyList<Entry>>();
			SeedOffset = Randomizer.Random.Next(int.MaxValue / 2); // Get a random seed based on time.
		}

		public IEnumerable<Point> GenerateXY()
		{
			for (var y = 0; y < GridSize; y++)
				for (var x = 0; x < GridSize; x++)
					yield return new Point(x, y);
		}

		public IEnumerable<Entry> GenerateOrdered()
			=> from eater in GenerateXY()
			   from food in GenerateXY()
			   where !eater.Equals(food)
			   select new Entry(eater, food);

		public IEnumerable<Entry> Generate(int seed)
		{
			var random = new Random(SeedOffset + seed);
			while (true)
			{
				var eater = RandomPosition(random);
				while (true)
				{
					var food = RandomPosition(random);
					if (food.Equals(eater)) continue;
					yield return new Entry(eater, food);
					break;
				}
			}
			// ReSharper disable once IteratorNeverReturns
		}

		public IEnumerable<Entry> Get(int id)
			=> id == -1 ? GenerateOrdered() : _sampleCache.GetOrAdd(id, key => Generate(id).Memoize(true));

		Point RandomPosition(Random random)
			=> new Point(random.Next(GridSize), random.Next(GridSize));


	}
}
