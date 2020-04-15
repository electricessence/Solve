using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Eater
{

	public partial class GenomeFactory : Solve.ReducibleGenomeFactoryBase<Genome>
	{
		public GenomeFactory(IEnumerable<Genome>? seeds = null, bool leftTurnDisabled = false) : base(seeds)
		{
			AvailableSteps = leftTurnDisabled ? Steps.ALL.Where(s => s != Step.TurnLeft).ToList().AsReadOnly() : Steps.ALL;
		}

		// ReSharper disable once UnusedParameter.Local
		public GenomeFactory(Genome seed, bool leftTurnDisabled = false) : this(seed == null ? default(IEnumerable<Genome>) : new[] { seed }, leftTurnDisabled)
		{

		}

		public static IEnumerable<string> Random(int moves, int maxMoveLength, bool leftTurnDisabled = false)
		{
			var size = moves * 2 - 1;
			var steps = new StepCount[size];
			var random = new Random();

			while (true)
			{
				for (var i = 0; i < size; i++)
				{
					steps[i] = (i % 2) switch
					{
						0 => new StepCount(Step.Forward, random.Next(maxMoveLength) + 1),
						1 => new StepCount(leftTurnDisabled ? Step.TurnRight : (random.Next(2) == 0 ? Step.TurnRight : Step.TurnLeft)),
						_ => throw new NotImplementedException()
					};
				}

				// Must start and end with foward movements.  Turns are wasted.
				Debug.Assert(steps[0].Step == Step.Forward);
				Debug.Assert(steps[size - 1].Step == Step.Forward);

				var hash = steps.AsSpan().ToGenomeHash();
				yield return hash;
			}
		}

		public readonly IReadOnlyList<Step> AvailableSteps;

		public int GeneratedCount { get; private set; }

		readonly LinkedList<StepCount> _lastGenerated = new LinkedList<StepCount>();
		readonly Random Randomizer = new Random();

		/*
         * The goal here is to produce unique eaters.
         */
		protected override Genome GenerateOneInternal()
		{
			lock (_lastGenerated)
			{
				var count = GeneratedCount;
				if (count == 0)
				{
					_lastGenerated.AddLast(Step.Forward);
				}
				else
				{
					_lastGenerated.AddLast(Step.TurnRight);
					_lastGenerated.AddLast(StepCount.Forward(Randomizer.Next(count * 2)));
				}

				++GeneratedCount;
				return new Genome(_lastGenerated);
			}
		}

		protected override Genome[] CrossoverInternal(Genome a, Genome b)
		{
			var aLen = a.Genes.Length;
			var bLen = b.Genes.Length;
			if (aLen == 0 || bLen == 0 || aLen == 1 && bLen == 1) return Array.Empty<Genome>();

			var aPoint = Randomizer.Next(aLen - 1) + 1;
			var bPoint = Randomizer.Next(bLen - 1) + 1;

			var aGenes = a.Genes.ToArray();
			var bGenes = b.Genes.ToArray();

			return new[]
			{
				new Genome(aGenes.Take(aPoint).Concat(bGenes.Skip(bPoint)).TrimTurns()),
				new Genome(bGenes.Take(bPoint).Concat(aGenes.Skip(aPoint)).TrimTurns()),
			};
		}

		protected override Genome MutateInternal(Genome target)
		{
			var genes = target.Genes.ToArray();
			var length = genes.Length;
			var index = Randomizer.Next(genes.Length);
			var value = genes[index];

			var stepCount = AvailableSteps.Count;
			// Select a replacement where one of the replacements is potentially 'blank' (to remove).
			var i = Randomizer.Next(length > 3 ? stepCount + 1 : stepCount);
			if (i == stepCount)
			{
				// Remove 1, 2, or 3?
				var r = Randomizer.Next(Math.Min(3, length / 6)) + 1;
				var asRemoved = Remove(genes, index, r).ToImmutableArray();
				var rlen = asRemoved.Length;
				var n = Randomizer.Next(rlen * 3); // 1 in 3 chances to 'swap' instead of remove.
				if (n == index) n++;
				return n < rlen
					? new Genome(Splice(asRemoved, n, genes.Skip(index).Take(r)).TrimTurns())
					: new Genome(asRemoved.TrimTurns());
			}

			// Replace or insert...
			var g = AvailableSteps[i];
			Genome Insert(Step s, int count)
				=> new Genome(Splice(genes, index, s, count).TrimTurns());

			// ReSharper disable once SwitchStatementMissingSomeCases
			switch (value)
			{
				case Step.Forward when g == Step.Forward:
					return Insert(g, Randomizer.Next(3) + 1);
				// Insert or replace
				case Step.Forward when Randomizer.Next(2) == 0:
					return Insert(g, 1);
				default:
					if (g == Step.Forward) // Op
					{
						if (Randomizer.Next(2) == 0) // Insert or replace
							return Insert(g, Randomizer.Next(3) + 1);
					}
					else if (g == value)
					{
						return Insert(g, 1);
					}

					break;
			}

			genes[index] = g;
			return new Genome(genes.TrimTurns());

		}

	}
}
