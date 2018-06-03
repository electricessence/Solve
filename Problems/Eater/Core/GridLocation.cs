using System;

namespace Eater
{
	public struct GridLocation : IEquatable<GridLocation>
	{
		public readonly uint X;
		public readonly uint Y;

		public GridLocation(uint x, uint y)
		{
			X = x;
			Y = y;
		}

		public GridLocation(int x, int y)
		{
			if (x < 0) throw new ArgumentOutOfRangeException(nameof(x), x, "Must be at least 0.");
			if (y < 0) throw new ArgumentOutOfRangeException(nameof(y), y, "Must be at least 0.");
			X = (uint)x;
			Y = (uint)y;
		}

		public bool Equals(GridLocation other)
		{
			return this.X == other.X && this.Y == other.Y;
		}
	}
}
