using Open.RandomizationExtensions;
using System;
using System.Buffers;
using System.Linq;

namespace Eater
{

	public partial class GenomeFactory
	{
		protected override Genome[] CrossoverInternal(Genome a, Genome b)
		{
			var aLen = a.Genes.Length;
			var bLen = b.Genes.Length;
			if (aLen == 0 || bLen == 0 || aLen == 1 && bLen == 1) return Array.Empty<Genome>();

			var rand = Randomizer.Random;
			var aPoint = rand.Next(aLen - 1) + 1;
			var bPoint = rand.Next(bLen - 1) + 1;

			var aGenes = a.Genes.ToArray();
			var bGenes = b.Genes.ToArray();

			return new[]
			{
				new Genome(aGenes.Take(aPoint).Concat(bGenes.Skip(bPoint)).TrimTurns()),
				new Genome(bGenes.Take(bPoint).Concat(aGenes.Skip(aPoint)).TrimTurns()),
			};
		}
	}
}
