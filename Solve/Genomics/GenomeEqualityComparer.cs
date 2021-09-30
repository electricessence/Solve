using System.Collections.Generic;

namespace Solve
{
	public class GenomeEqualityComparer<TGenome> : EqualityComparer<TGenome>
		where TGenome : IGenome?
	{
		public override bool Equals(TGenome? x, TGenome? y) => x?.Hash == y?.Hash;
		public override int GetHashCode(TGenome genome) => genome?.Hash.GetHashCode() ?? 0;

		public static readonly GenomeEqualityComparer<TGenome> Instance = new();
	}
}
