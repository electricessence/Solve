using System.Collections.Generic;
using System.Linq;

namespace Eater
{
	public partial class GenomeFactory
	{
		static readonly IEnumerable<Step> ForwardOne = Enumerable.Repeat(Step.Forward, 1);

		public static IEnumerable<IEnumerable<Step>> GetVariations(Step[] source)
		{
			yield return source.Reverse();

			var lenMinusOne = source.Length - 1;
			if (lenMinusOne > 1)
			{
				IEnumerable<Step> removeTail = source.Take(lenMinusOne).ToList();
				yield return removeTail;
				yield return Enumerable.Repeat(source[lenMinusOne], 1).Concat(removeTail);

				IEnumerable<Step> removeHead = source.Skip(1).ToList();
				yield return removeHead;
				yield return removeHead.Concat(Enumerable.Repeat(source[0], 1));

			}

			// All forward movement lengths reduced by 1.
			yield return source.ToStepCounts().Select(sc => sc.Step == Step.Forward && sc.Count > 1 ? --sc : sc).Steps();


			yield return ForwardOne.Concat(source);
			yield return source.Concat(ForwardOne);

			// All forward movement lengths doubled...
			yield return source.SelectMany(g => Enumerable.Repeat(g, g == Step.Forward ? 2 : 1));

			// Pattern is doubled.
			yield return Enumerable.Repeat(source, 2).SelectMany(s => s);

			var half = source.Length / 2;
			if (half <= 2) yield break;
			yield return source.Take(half);
			yield return source.Skip(half);

			var third = source.Length / 3;
			if (third <= 2) yield break;
			yield return source.Take(third);
			yield return source.Skip(third).Take(third);
			yield return source.Skip(third);
			yield return source.Take(2 * third);
			yield return source.Skip(2 * third);
		}

		protected override IEnumerable<Genome> GetVariationsInternal(Genome source)
			=> GetVariations(source.Genes.ToArray())
				.Concat(base.GetVariationsInternal(source) ?? Enumerable.Empty<Genome>())
				.Select(steps => new Genome(steps));
	}
}
