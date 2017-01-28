using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Open;
using Open.Collections;

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

		public SampleCache(int gridSize = 10)
		{
			if (gridSize < 2)
				throw new ArgumentOutOfRangeException("gridSize", gridSize, "Must be at least 2.");
			GridSize = gridSize;
			GridSizeMid = gridSize / 2;
			Boundary = new GridLocation(gridSize, gridSize);

			_sampleCache = new ConcurrentDictionary<long, LazyList<Entry>>();
		}

		public IEnumerable<Entry> Generate()
		{
			while (true)
			{
				var eater = RandomQuadrantPosition();

				// Make sure the food is in the opposite quadrant of the eater. +/- 1 to avoid middle.
				var foodX = RandomUtilities.Random.Next(GridSizeMid - 1);
				var foodY = RandomUtilities.Random.Next(GridSizeMid - 1);
				if (eater.X < GridSizeMid) foodX += GridSizeMid + 1;
				if (eater.Y < GridSizeMid) foodY += GridSizeMid + 1;

				yield return new Entry(eater, new GridLocation(foodX, foodY));
			}
		}

		public LazyList<Entry> Get(long id)
		{
			return _sampleCache.GetOrAdd(id, key => Generate().Memoize(true));
		}

		GridLocation RandomQuadrantPosition()
		{
			GridLocation result;
			do
			{
				result = new GridLocation(RandomUtilities.Random.Next(GridSize), RandomUtilities.Random.Next(GridSize));
			}
			while (result.X == GridSizeMid || result.Y == GridSizeMid);
			return result;
		}


	}
}