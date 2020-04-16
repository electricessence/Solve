using Open.Collections;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace Eater
{
	public partial class GenomeFactory
	{
		public static IEnumerable<IEnumerable<Step>> GetVariations(IReadOnlyList<Step> source)
		{
			// Cut lenghts in half.
			var stepCounts = source.ToStepCounts().ToArray();
			var stepCount = stepCounts.Length;
			foreach (var i in Enumerable.Range(0, stepCount).Shuffle())
			{
				var step = stepCounts[i];
				var head = stepCounts.Take(i).Steps();
				var tail = stepCounts.Skip(i + 1).Steps();

				// Remove one.
				yield return head
					.Concat(tail);

				if (step.Step != Step.Forward) continue;

				// Double a length.
				yield return head
					.Concat(StepCount.Forward(step.Count * 2))
					.Concat(tail);

				if (step.Count < 2) continue;

				// Half a length.
				yield return head
					.Concat(StepCount.Forward(step.Count / 2))
					.Concat(tail);

				// Add one.
				yield return head
					.Concat(StepCount.Forward(step.Count + 1))
					.Concat(tail);

				// Add one.
				yield return head
					.Concat(StepCount.Forward(step.Count - 1))
					.Concat(tail);
			}

			yield return source.Reverse();

			// Pattern is doubled.
			yield return Enumerable.Repeat(source, 2).SelectMany(s => s);

			// All forward movement lengths reduced by 1.
			yield return stepCounts.Select(sc => sc.Step == Step.Forward && sc.Count > 1 ? --sc : sc).Steps();

			// All forward movement lengths doubled...
			yield return source.SelectMany(g => Enumerable.Repeat(g, g == Step.Forward ? 2 : 1));

			var len = source.Count;
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
				.Select(steps => new Genome(steps.TrimTurns()));
	}
}
