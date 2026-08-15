using Open.Collections;
using Open.Disposable;
using Open.Text;
using Solve;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;

namespace Eater;

public partial class GenomeFactory
{
	static readonly Regex UTurn = UTurnPattern();
	static readonly Regex Loop = LoopPattern();

	// 10-0002: randomness source is now a required parameter (there are no external callers of
	// this method today -- see the sole caller below) so the Shuffle() draw always routes
	// through the factory's injected/seedable RandomSource rather than an unseedable ambient
	// default.
	public static IEnumerable<IEnumerable<Step>> GetVariations(IReadOnlyList<Step> source, Random random)
	{
		ArgumentNullException.ThrowIfNull(random);
		int len = source.Count;
		// Try to simply shorten the result first.
		yield return source.Take(len - 1); // by 1
		int half = len / 2;
		if (half > 2) yield return source.Take(half); // by half

		var stepCounts = source.ToStepCounts().ToArray();
		int stepCount = stepCounts.Length;

		string hash = stepCounts.Steps().ToGenomeHash();
		var matches = UTurn.Matches(hash);
		var sb = StringBuilderPool.Shared.Take();
		foreach (var match in matches.Cast<Match>())
		{
			yield return Steps.FromGenomeHash(sb.Clear()
				.Append(hash.AsSpan(0, match.Index))
				.Append(match.AsSpan().Trim('^'))
				.Append(hash.AsSpan(match.Index + match.Length))
				.ToString());
		}

		StringBuilderPool.Shared.Give(sb);

		yield return source.Reverse();

		foreach (int i in Enumerable.Range(0, stepCount).Shuffle(random))
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

		// All forward movement lengths reduced by 1.
		yield return stepCounts.Select(sc => sc.Step == Step.Forward && sc.Count > 1 ? --sc : sc).Steps();

		// All forward movement lengths doubled...
		yield return source.SelectMany(g => Enumerable.Repeat(g, g == Step.Forward ? 2 : 1));

		// Pattern is doubled.
		yield return Enumerable.Repeat(source, 2).SelectMany(s => s);

		if (half <= 2) yield break;
		yield return source.Skip(half);

		int third = len / 3;
		if (third <= 2) yield break;
		yield return source.Take(third);
		yield return source.Skip(third).Take(third);
		yield return source.Skip(third);
		yield return source.Take(2 * third);
		yield return source.Skip(2 * third);

		matches = Loop.Matches(hash);
		foreach (var match in matches.Cast<Match>())
		{
			yield return Steps.FromGenomeHash(
				string.Concat(
					hash.AsSpan(0, match.Index),
					match.Value.Replace("^", string.Empty),
					hash.AsSpan(match.Index + match.Length)));
		}
	}

	protected override IEnumerable<Genome> GetVariationsInternal(Genome source)
		// 10-0005: the coverage-preserving reduction (GetReduced, overridden in
		// GenomeFactory.Reduce.cs) is tried first, ahead of the syntactic variation catalogue
		// below -- otherwise it would only be reached once that (potentially very long, for
		// large champions) catalogue is fully exhausted.
		=> (base.GetVariationsInternal(source) ?? [])
			.Concat(GetVariations(source.Genes.ToArray(), RandomSource))
			.Select(steps => steps.TrimTurns().ToImmutableArray())
			// Some candidates (e.g. "remove one" on a minimal genome) can legitimately
			// reduce to nothing after trimming; skip those rather than let Genome's Freeze
			// throw on an empty step sequence.
			.Where(steps => steps.Length != 0)
			.Select(steps => new Genome(steps));

	[GeneratedRegex(@"\^([<>])\1\^", RegexOptions.Compiled)]
	private static partial Regex UTurnPattern();

	[GeneratedRegex(@"(\^[<>])\1{2}\^", RegexOptions.Compiled)]
	private static partial Regex LoopPattern();
}
