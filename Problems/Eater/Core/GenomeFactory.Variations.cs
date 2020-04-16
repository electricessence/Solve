using Open.Collections;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace Eater
{
	public partial class GenomeFactory
	{
		static readonly IEnumerable<Step> ForwardOne = StepCount.Forward(1);

		public static IEnumerable<IEnumerable<Step>> GetVariations(IReadOnlyList<Step> source)
		{
			// Cut lenghts in half.
			var stepCounts = source.ToStepCounts().ToArray();
			var stepCount = stepCounts.Length;
			foreach (var i in Enumerable.Range(0, stepCount).Shuffle())
			{
				var step = stepCounts[i];

				if (step.Step != Step.Forward) continue;
				var head = stepCounts.Take(i).Steps();
				var tail = stepCounts.Skip(i + 1).Steps();

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
			}

			yield return source.Reverse();

			// Pattern is doubled.
			yield return Enumerable.Repeat(source, 2).SelectMany(s => s);

			var len = source.Count;
			var lenMinusOne = len - 1;
			//if (lenMinusOne > 1)
			//{
			//	var removeTail = source.Take(lenMinusOne);
			//	yield return removeTail.TrimTurns();

			//	var removeHead = source.Skip(1);
			//	yield return removeHead.TrimTurns();
			//}

			////yield return ForwardOne.Concat(source);
			////yield return source.Concat(ForwardOne);

			//// All forward movement lengths reduced by 1.
			//yield return stepCounts.Select(sc => sc.Step == Step.Forward && sc.Count > 1 ? --sc : sc).Steps();

			//// Remove step at every point.
			//if (lenMinusOne > 2)
			//{
			//	for (var i = 1; i < len - 2; i++)
			//	{
			//		var head = source.Take(i);
			//		var tail = source.Skip(i + 1);
			//		yield return head.Concat(tail);
			//	}
			//}

			//// All forward movement lengths doubled...
			//yield return source.SelectMany(g => Enumerable.Repeat(g, g == Step.Forward ? 2 : 1));



			//var half = len / 2;
			//if (half <= 2) yield break;
			//yield return source.Take(half);
			//yield return source.Skip(half);

			//var third = len / 3;
			//if (third <= 2) yield break;
			//yield return source.Take(third);
			//yield return source.Skip(third).Take(third);
			//yield return source.Skip(third);
			//yield return source.Take(2 * third);
			//yield return source.Skip(2 * third);
		}

		protected override IEnumerable<Genome> GetVariationsInternal(Genome source)
			=> GetVariations(source.Genes.ToArray())
				.Concat(base.GetVariationsInternal(source) ?? Enumerable.Empty<Genome>())
				.Select(steps => new Genome(steps.TrimTurns()));
	}
}
