using Open.Collections;
using Solve;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Eater
{
	public partial class GenomeFactory
	{
		static readonly Regex UTurn = new Regex(@"\^([<>])\1\^", RegexOptions.Compiled);
		static readonly Regex Loop = new Regex(@"(\^[<>])\1{2}\^", RegexOptions.Compiled);

		public static IEnumerable<IEnumerable<Step>> GetVariations(IReadOnlyList<Step> source)
		{
			var stepCounts = source.ToStepCounts().ToArray();
			var stepCount = stepCounts.Length;

			var hash = stepCounts.Steps().ToGenomeHash();
			var matches = UTurn.Matches(hash);
			foreach(var match in matches.Cast<Match>())
			{
				yield return Steps.FromGenomeHash(
					hash.Substring(0, match.Index) +
					match.Value.Trim('^') +
					hash.Substring(match.Index + match.Length));
			}

			matches = Loop.Matches(hash);
			foreach (var match in matches.Cast<Match>())
			{
				yield return Steps.FromGenomeHash(
					hash.Substring(0, match.Index) +
					match.Value.Replace("^", string.Empty) +
					hash.Substring(match.Index + match.Length));
			}

			foreach (var i in Enumerable.Range(0, stepCount).Shuffle())
			{
				var segments = SplicedEnumerable.Create(stepCounts.Take(i).Steps(), stepCounts.Skip(i + 1).Steps());
				var step = stepCounts[i];

				// Remove one.
				yield return segments;

				if (step.Step != Step.Forward) continue;

				// Double a length.
				yield return segments.InsertSegment(StepCount.Forward(step.Count * 2));

				if (step.Count < 2) continue;

				// Half a length.
				yield return segments.InsertSegment(StepCount.Forward(step.Count / 2));

				// Add one.
				yield return segments.InsertSegment(StepCount.Forward(step.Count + 1));

				// Remove one.
				yield return segments.InsertSegment(StepCount.Forward(step.Count - 1));
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
