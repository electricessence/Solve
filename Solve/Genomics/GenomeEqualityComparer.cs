using System.Collections.Generic;

namespace Solve
{
	public class GenomeEqualityComparer : EqualityComparer<IGenome>
	{
		public override bool Equals(IGenome x, IGenome y)
		{
			if (x == y) return true;
			if (x == null || y == null) return false;
			return x.Hash == y.Hash;
		}

		public override int GetHashCode(IGenome genome)
			=> genome.Hash.GetHashCode();

		public static readonly GenomeEqualityComparer Instance = new GenomeEqualityComparer();
	}
}
