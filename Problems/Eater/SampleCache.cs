using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Open;
using Open.Collections;
using Open.Arithmetic;
using Open.Numeric;

namespace Eater
{
	public sealed class SampleCache
	{
		public struct Entry
		{
			public readonly GridLocation EaterStart;
			public readonly GridLocation Food;

			public Entry(GridLocation eaterStart, GridLocation food)
			{
				EaterStart = eaterStart;
				Food = food;
			}
		}

		readonly ConcurrentDictionary<long, LazyList<Entry>> _sampleCache;

		public readonly int GridSize;
		public readonly int GridSizeMid;

		public readonly GridLocation Boundary;
		public int SeedOffset;

		public SampleCache(int gridSize = 10)
		{
			if (gridSize < 2)
				throw new ArgumentOutOfRangeException("gridSize", gridSize, "Must be at least 2.");
			GridSize = gridSize;
			GridSizeMid = gridSize / 2;
			Boundary = new GridLocation(gridSize, gridSize);

			_sampleCache = new ConcurrentDictionary<long, LazyList<Entry>>();
			SeedOffset = RandomUtilities.Random.Next(int.MaxValue / 2); // Get a random seed based on time.
		}

		public IEnumerable<GridLocation> GenerateXY()
		{
			for (var y = 0; y < GridSize; y++)
			{
				for (var x = 0; x < GridSize; x++)
				{
					yield return new GridLocation(x, y);
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

		public ProcedureResult[] TestAll(EaterGenome genome)
		{
			return TestAll(genome.Hash);
		}

			public LazyList<Entry> Get(int id)
		{
			return _sampleCache.GetOrAdd(id, key => Generate(id).Memoize(true));
		}

		GridLocation RandomPosition(Random random)
		{
			return new GridLocation(random.Next(GridSize), random.Next(GridSize));
		}


	}
}