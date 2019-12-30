using Open.Memory;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace Eater
{
	public partial class GenomeFactory
	{
		static readonly IEnumerable<Step> ForwardOne = Enumerable.Repeat(Step.Forward, 1);

		public static IEnumerable<IEnumerable<Step>> GetVariations(IReadOnlyList<Step> source)
		{
			yield return source.Reverse();

			var len = source.Count;
			var lenMinusOne = len - 1;
			if (lenMinusOne > 1)
			{
				IEnumerable<Step> removeTail = source.Take(lenMinusOne).ToArray();
				yield return removeTail;
				yield return Enumerable.Repeat(source[lenMinusOne], 1).Concat(removeTail);

				IEnumerable<Step> removeHead = source.Skip(1).ToArray();
				yield return removeHead;
				yield return removeHead.Concat(Enumerable.Repeat(source[0], 1));
			}

			// All forward movement lengths reduced by 1.
			yield return source.ToStepCounts().Select(sc => sc.Step == Step.Forward && sc.Count > 1 ? --sc : sc).Steps();

			// Remove step at every point.
			if (lenMinusOne > 2)
			{
				for (var i = 1; i < len - 2; i++)
				{
					var head = source.Take(i);
					var tail = source.Skip(i + 1);
					yield return head.Concat(tail);
				}
			}

			// Insert forward movement at every point.
			for (var i = 0; i < len; i++)
			{
				var head = source.Take(i);
				var tail = source.Skip(i);
				yield return head.Concat(ForwardOne).Concat(tail);
			}

			// All forward movement lengths doubled...
			yield return source.SelectMany(g => Enumerable.Repeat(g, g == Step.Forward ? 2 : 1));

			// Pattern is doubled.
			yield return Enumerable.Repeat(source, 2).SelectMany(s => s);

			var half = len / 2;
			if (half <= 2) yield break;
			yield return source.Take(half);
			yield return source.Skip(half);

			var third = len / 3;
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
