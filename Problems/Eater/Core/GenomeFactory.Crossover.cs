using System;
using System.Linq;

namespace Eater;

public partial class GenomeFactory
{
	protected override Genome[] CrossoverInternal(Genome a, Genome b)
	{
		int aLen = a.Genes.Length;
		int bLen = b.Genes.Length;
		if (aLen == 0 || bLen == 0 || aLen == 1 && bLen == 1) return [];

		Random rand = System.Random.Shared;
		int aPoint = rand.Next(aLen - 1) + 1;
		int bPoint = rand.Next(bLen - 1) + 1;

		return
		[
			new Genome(a.Genes.Take(aPoint).Concat(b.Genes.Skip(bPoint)).TrimTurns()),
			new Genome(b.Genes.Take(bPoint).Concat(a.Genes.Skip(aPoint)).TrimTurns()),
		];
	}
}
