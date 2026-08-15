using Solve;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Eater;

public partial class GenomeFactory
{
	// ---- 10-0005: coverage-preserving reduction --------------------------------------------
	//
	// Blind gene removal (see GenomeFactory.Mutate.cs / GetVariations in
	// GenomeFactory.Variations.cs) has to get lucky: any edit that drops a sample's
	// Food-Found-Rate to failure is immediately non-competitive, since every configured pool
	// ranks Food-Found-Rate first. As champions approach efficiency, an increasing share of
	// blind removals fail at least one sample, so gene-count improvement decelerates
	// asymptotically (observed: ~224 genes on a 10x10 grid vs. a hand-derived ~138).
	//
	// Two complementary, deterministic passes look for segments that are safe to drop, and
	// both are verified the same way: by re-running the actual simulation (Steps.Try) against
	// every sample the input genome already solved, committing a candidate removal only if
	// every one of them still succeeds (10-0005 AC2) -- neither pass is trusted on its own.
	//
	//  1. Closed-loop detection (CollectStateLoops): a contiguous run of steps that returns
	//     the walker to the exact (position, orientation) state it had before the run started
	//     is a free cut in a single simulation -- the walker resumes identically either way.
	//     Cheap, and catches the "obvious" case (a segment that retraces already-visited
	//     cells and undoes its own turns), but an already mutation-refined champion has
	//     usually had this simple a redundancy bred out of it already.
	//  2. Delta-debugging (DeltaDebugMinimize): a classic minimizing search (Zeller's ddmin)
	//     that tries removing progressively smaller contiguous chunks, verifying each one
	//     empirically. It has no geometric proof behind any single candidate -- it is exactly
	//     the "for example" heuristic in this task's Objective, generalized to chunks larger
	//     than blind mutation's 1-3 gene edits -- but the same Try-based verification gate
	//     means it can never commit an unsafe cut. This is what actually shrinks real
	//     champions in practice; see the benchmark comparison recorded in this task's
	//     completion notes.

	/// <summary>
	/// Repeatedly finds and removes the largest segment of <paramref name="genome"/>'s steps
	/// that can be shown redundant against <paramref name="boundary"/> + <paramref name="samples"/>
	/// (see remarks above), until no further removal is possible (a fixed point).
	/// </summary>
	/// <param name="genome">The genome to reduce. Never mutated in place; a new, shorter
	/// <see cref="Genome"/> is returned when a reduction is found, otherwise <paramref name="genome"/>
	/// itself (same reference).</param>
	/// <param name="boundary">The grid boundary steps are simulated against (see <see cref="Steps.Try"/>).</param>
	/// <param name="samples">Start/food pairs used both to discover candidate redundant
	/// segments (every distinct sample start contributes its own state trace for the
	/// closed-loop pass) and to verify that a candidate removal is safe: every sample the
	/// input genome currently solves must still be solved by the candidate before it is
	/// committed.</param>
	/// <returns>
	/// A genome whose gene count is less than or equal to <paramref name="genome"/>'s (10-0005 AC1),
	/// which still succeeds on every sample <paramref name="genome"/> succeeded on (10-0005 AC2), and
	/// which is a fixed point of this same operation: reducing the result again returns a genome
	/// with the same hash (10-0005 AC3).
	/// </returns>
	public static Genome ReduceCoverage(Genome genome, Size boundary, IEnumerable<SampleCache.Entry> samples)
	{
		ArgumentNullException.ThrowIfNull(genome);
		ArgumentNullException.ThrowIfNull(samples);

		IReadOnlyList<SampleCache.Entry> sampleList = samples as IReadOnlyList<SampleCache.Entry> ?? [.. samples];

		ImmutableArray<Step> current = genome.Genes;

		// Only samples the *original* genome already solves are required to keep succeeding
		// (10-0005 AC2) -- a sample it already failed is free to keep failing.
		List<SampleCache.Entry> required = [];
		foreach (SampleCache.Entry s in sampleList)
		{
			if (current.Try(boundary, s.EaterStart, s.Food))
				required.Add(s);
		}

		if (required.Count == 0)
			return genome; // Nothing to preserve; leave the genome untouched rather than guess.

		Point[] traceStarts = [.. sampleList.Select(s => s.EaterStart).Distinct()];

		bool Solves(ImmutableArray<Step> candidate)
			=> candidate.Length != 0
				&& candidate[0] == Step.Forward && candidate[^1] == Step.Forward
				&& required.All(s => candidate.Try(boundary, s.EaterStart, s.Food));

		bool improved = true;
		while (improved)
		{
			improved = false;

			// Pass 1: cheap, exact closed-loop candidates.
			var candidates = new HashSet<(int Start, int Length)>();
			foreach (Point traceStart in traceStarts)
				CollectStateLoops(current, boundary, traceStart, candidates);

			// Largest-first: prefer the biggest verified-safe cut each pass. Capped so a
			// genome with many candidate loops (e.g. a long oscillating turn run) can't blow
			// up per-pass verification cost -- this only bounds thoroughness (still a "greedy
			// fixed-point reduction," which is all task 10-0005 requires), never correctness.
			IEnumerable<(int Start, int Length)> ordered = candidates
				.OrderByDescending(c => c.Length)
				.ThenBy(c => c.Start)
				.Take(64);

			foreach ((int start, int length) in ordered)
			{
				if (length <= 0 || length >= current.Length) continue;

				// Syntactic cleanup (Reduce/TrimTurns, both existing Step.cs logic, unmodified
				// here) can occasionally invalidate a raw removal (e.g. dropping a leading turn
				// that mattered) -- Solves()'s Try verification is what actually guards
				// correctness, this is just the usual cleanup step.
				ImmutableArray<Step> trial = RemoveSegment(current, start, length)
					.Reduce().TrimTurns().ToImmutableArray();

				if (trial.Length >= current.Length || !Solves(trial)) continue;

				current = trial;
				improved = true;
				break; // Re-derive candidates against the shrunk genome before trying more.
			}

			if (improved) continue;

			// Pass 2: no exact loop found -- fall back to a delta-debugging chunk search.
			// This is what actually shrinks genomes that mutation has already refined (no
			// segment returns to a literal prior state, but a chunk can still be cut and
			// verified safe).
			ImmutableArray<Step> ddResult = DeltaDebugMinimize(current, Solves);
			if (ddResult.Length < current.Length)
			{
				current = ddResult;
				improved = true;
			}
		}

		return current.Length == genome.Genes.Length ? genome : new Genome(current);
	}

	/// <summary>
	/// Walks <paramref name="steps"/> from <paramref name="start"/> (orientation
	/// <see cref="Orientation.Up"/>, matching <see cref="Steps.Try"/>'s own simulation), recording
	/// the (position, orientation) state before every step. Whenever a state repeats, the steps
	/// between the two occurrences form a closed loop: executing them returns the walker to a
	/// state identical to where it started, so removing them cannot affect anything before or
	/// after that point in *this* simulation. Every such (start index, length) pair is added to
	/// <paramref name="candidates"/> as worth trying -- <see cref="ReduceCoverage"/> is what
	/// actually verifies and commits them.
	/// </summary>
	static void CollectStateLoops(
		ImmutableArray<Step> steps, Size boundary, Point start,
		HashSet<(int Start, int Length)> candidates)
	{
		var seen = new Dictionary<(Point Position, Orientation Orientation), int>();
		Point position = start;
		Orientation orientation = Orientation.Up;

		void Record(int index)
		{
			(Point Position, Orientation Orientation) key = (position, orientation);
			if (seen.TryGetValue(key, out int firstIndex))
				candidates.Add((firstIndex, index - firstIndex));
			else
				seen[key] = index;
		}

		Record(0);
		for (int i = 0; i < steps.Length; i++)
		{
			switch (steps[i])
			{
				case Step.Forward:
					position = boundary.Forward(position, orientation);
					break;
				case Step.TurnLeft:
					orientation = orientation.TurnLeft();
					break;
				case Step.TurnRight:
					orientation = orientation.TurnRight();
					break;
			}

			Record(i + 1);
		}
	}

	/// <summary>
	/// A delta-debugging-style minimizing sweep (in the spirit of Zeller's ddmin, simplified for
	/// bounded cost -- see remarks below): tries to cut out contiguous chunks at progressively
	/// finer granularity (starting coarse -- half the sequence -- doubling the chunk count each
	/// pass), verifying every candidate cut via <paramref name="solves"/> before committing it,
	/// so (unlike the closed-loop pass) no geometric justification is needed for any individual
	/// candidate -- it is a systematic generalization of blind mutation's single-point removal,
	/// checked exhaustively instead of randomly.
	/// </summary>
	/// <remarks>
	/// Classic ddmin resets to coarse granularity after every successful cut, re-scanning from
	/// the top; that is more thorough but, run synchronously once per distinct champion in the
	/// live pipeline, was expensive enough (observed: a large fraction of a 3-minute benchmark
	/// smoke's time budget) to starve normal evaluation throughput and net out *worse* champions
	/// than no reduction at all. This version instead does a single left-to-right sweep per
	/// granularity level, applying successful cuts in place and continuing rather than
	/// restarting, which bounds total candidate trials to roughly O(genome length) instead of
	/// unbounded. <see cref="ReduceCoverage"/>'s own outer fixed-point loop calls this again
	/// (cheaply, against the now-shorter genome) whenever it makes progress, so thoroughness
	/// lost to not restarting here is largely recovered across outer iterations instead.
	/// </remarks>
	static ImmutableArray<Step> DeltaDebugMinimize(ImmutableArray<Step> steps, Func<ImmutableArray<Step>, bool> solves)
	{
		for (int n = 2; steps.Length >= 2 && n <= steps.Length; n *= 2)
		{
			int chunkSize = Math.Max(1, (steps.Length + n - 1) / n); // ceil(len / n)
			int i = 0;
			while (i < steps.Length)
			{
				int length = Math.Min(chunkSize, steps.Length - i);
				if (length >= steps.Length) break; // Would remove everything -- never valid.

				ImmutableArray<Step> trial = RemoveSegment(steps, i, length)
					.Reduce().TrimTurns().ToImmutableArray();

				if (trial.Length < steps.Length && solves(trial))
				{
					// Content past the cut shifted left into position i; rescan from there
					// rather than restarting the whole sweep.
					steps = trial;
					if (steps.Length < 2) return steps;
				}
				else
				{
					i += chunkSize;
				}
			}

			if (chunkSize <= 1) break; // Already at the finest possible granularity.
		}

		return steps;
	}

	static ImmutableArray<Step> RemoveSegment(ImmutableArray<Step> steps, int start, int length)
	{
		ImmutableArray<Step>.Builder builder = ImmutableArray.CreateBuilder<Step>(steps.Length - length);
		for (int i = 0; i < start; i++) builder.Add(steps[i]);
		for (int i = start + length; i < steps.Length; i++) builder.Add(steps[i]);
		return builder.MoveToImmutable();
	}

	// ---- Factory wiring (10-0005 AC4) -------------------------------------------------------
	//
	// GenomeFactory doesn't otherwise know its paired problem's grid size or sample
	// distribution -- the factory and its Problem are constructed independently everywhere in
	// this repo (see e.g. Problems/Eater/Benchmark/Program.cs). The 10x10 default below matches
	// every current Eater entry point's own default grid size (Problem.Create*,
	// Benchmark/Program.cs, Console/Runner.cs, ExperimentRun.cs), so the built-in champion
	// reduction wiring is meaningful out of the box without requiring those call sites
	// (several of which are out of scope for this change) to be touched.
	const ushort DefaultReductionGridSize = 10;
	static readonly Size DefaultReductionBoundary = new(DefaultReductionGridSize, DefaultReductionGridSize);

	// Measured trade-off: verification cost is O(sample count) per candidate cut, and
	// GetReduced runs synchronously once per distinct champion in the live pipeline, so this
	// has to stay small -- but too small overfits (a genome "reduced" against only a handful
	// of samples can shed genes it actually needs almost everywhere else on the grid, and then
	// just fails the pipeline's own much larger per-level evaluation, wasting the cycle instead
	// of helping). 150 pseudo-random samples reproduced the same result as an exhaustive
	// 9900-pair check on a real champion (~200 genes either way) in ~15-20ms; smaller counts
	// (e.g. 12-40) measurably over-reduced (as low as 69-166 genes, i.e. genuinely unsafe
	// outside the thin sample set they were checked against).
	const int DefaultReductionSampleCount = 150;

	// A fixed local seed (rather than SampleCache.Generate's process-randomized SeedOffset)
	// keeps the default sample set -- and therefore GetReduced's behavior -- reproducible
	// across runs. Deliberately *not* a strided walk of GenerateOrdered()'s nested-loop pairs:
	// striding that ordering produced structurally skewed subsets where, counter-intuitively,
	// *more* samples could mean *more* aggressive (i.e. less trustworthy) reduction.
	static readonly ImmutableArray<SampleCache.Entry> DefaultReductionSamples = BuildDefaultReductionSamples();

	static ImmutableArray<SampleCache.Entry> BuildDefaultReductionSamples()
	{
		var random = new Random(0x0EA7E5); // Arbitrary fixed seed ("EATER" in hex-ish leetspeak).
		var seen = new HashSet<(Point Start, Point Food)>();
		var list = new List<SampleCache.Entry>(DefaultReductionSampleCount);

		while (list.Count < DefaultReductionSampleCount)
		{
			var start = new Point(random.Next(DefaultReductionGridSize), random.Next(DefaultReductionGridSize));
			var food = new Point(random.Next(DefaultReductionGridSize), random.Next(DefaultReductionGridSize));
			if (start == food || !seen.Add((start, food))) continue;
			list.Add(new SampleCache.Entry(start, food));
		}

		return [.. list];
	}

	// GetReduced (below) is consulted from two places: once per genome when its variations are
	// enumerated (cheap -- see the ConditionalWeakTable-cached enumerator in
	// GenomeFactoryBase.GetVariations), but also from CannotCrossover on *every* breeding
	// attempt (ReducibleGenomeFactoryBase.CannotCrossover calls GetReduced on both candidates
	// to check whether they'd reduce to the same genome) -- and breeding attempts happen far
	// more often than champion broadcasts (PriorityQueue.ProcessBreeder alone retries up to 64
	// pairs per call). Without memoizing here, every one of those crossover checks would pay a
	// full ReduceCoverage pass; this table makes the expensive computation run at most once per
	// distinct genome instance no matter how many times it's consulted.
	static readonly ConditionalWeakTable<Genome, StrongBox<Genome?>> ReductionCache = [];

	/// <summary>
	/// Hook consulted by <see cref="ReducibleGenomeFactoryBase{TGenome}"/>: once per genome,
	/// when its variations are first enumerated (see the reordered concat in
	/// GenomeFactory.Variations.cs, which tries this ahead of the syntactic variation catalogue
	/// so it isn't starved by very long candidate lists on large champions) and again whenever
	/// two genomes are compared for breeding compatibility (<c>CannotCrossover</c>) -- covering
	/// both "champions" and "breeding stock" per 10-0005's scope boundary. A reduced result gets
	/// registered as a new production the next time it is pulled from the pipeline (10-0005 AC4);
	/// existing mutation behavior (GenomeFactory.Mutate.cs) is untouched. Memoized per genome
	/// instance (see <see cref="ReductionCache"/>) since this is on the crossover hot path.
	/// </summary>
	protected override Genome? GetReduced(Genome source)
	{
		if (source is null) return null;

		StrongBox<Genome?> box = ReductionCache.GetValue(source, static s =>
		{
			Genome reduced = ReduceCoverage(s, DefaultReductionBoundary, DefaultReductionSamples);
			return new StrongBox<Genome?>(ReferenceEquals(reduced, s) ? null : reduced);
		});

		return box.Value;
	}
}
