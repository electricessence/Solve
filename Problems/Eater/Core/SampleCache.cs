using Open.Collections;
using Open.Numeric;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;

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

		public readonly GridLocation Boundary;
		public int SeedOffset;

		public SampleCache(in ushort gridSize = 10)
		{
			if (gridSize < 2)
				throw new ArgumentOutOfRangeException(nameof(gridSize), gridSize, "Must be at least 2.");
			GridSize = gridSize;
			Boundary = new GridLocation(gridSize, gridSize);

			_sampleCache = new ConcurrentDictionary<long, LazyList<Entry>>();
			SeedOffset = RandomUtilities.Random.Next(int.MaxValue / 2); // Get a random seed based on time.
		}

		public IEnumerable<Point> GenerateXY()
		{
			for (var y = 0; y < GridSize; y++)
			{
				for (var x = 0; x < GridSize; x++)
				{
					yield return new Point(x, y);
				}
			}
		}

		public IEnumerable<Entry> GenerateOrdered()
		{
			foreach (var eater in GenerateXY())
			{
				foreach (var food in GenerateXY())
				{
					if (!eater.Equals(food))
						yield return new Entry(eater, food);
				}
			}
		}

		public IEnumerable<Entry> Generate(int seed)
		{
			var random = new Random(SeedOffset + seed);
			while (true)
			{
				var eater = RandomPosition(random);
				while (true)
				{
					var food = RandomPosition(random);
					if (!food.Equals(eater))
					{
						yield return new Entry(eater, food);
						break;
					}
				}
			}
		}

		public ProcedureResult[] TestAll(string genome)
		{
			double found = 0;
			double energy = 0;
			int count = 0;
			foreach (var entry in GenerateOrdered())
			{
				if (Steps.Try(genome, Boundary, entry.EaterStart, entry.Food, out int e))
					found++;
				energy += e;
				count++;
			}

			return new ProcedureResult[] {
				new ProcedureResult(found, count),
				new ProcedureResult(- energy, count),
				new ProcedureResult(- genome.Length * count, count)
			};
		}

		public ProcedureResult[] TestAll(Genome genome)
		{
			return TestAll(genome.Hash);
		}

		public LazyList<Entry> Get(int id)
		{
			return _sampleCache.GetOrAdd(id, key => Generate(id).Memoize(true));
		}

		Point RandomPosition(Random random)
		{
			return new Point(random.Next(GridSize), random.Next(GridSize));
		}


	}
}
