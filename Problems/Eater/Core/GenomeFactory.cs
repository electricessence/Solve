using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Eater
{

	public partial class GenomeFactory : Solve.ReducibleGenomeFactoryBase<Genome>
	{
		public GenomeFactory(IEnumerable<Genome> seeds = null, bool leftTurnDisabled = false) : base(seeds)
		{
			LeftTurnDisabled = leftTurnDisabled;
			AvailableSteps = leftTurnDisabled ? Steps.ALL.Where(s => s != Step.TurnLeft).ToList().AsReadOnly() : Steps.ALL;
		}

		// ReSharper disable once UnusedParameter.Local
		public GenomeFactory(Genome seed, bool leftTurnDisabled = false) : this(seed == null ? default(IEnumerable<Genome>) : new[] { seed })
		{

		}

		public static IEnumerable<string> Random(int moves, int maxMoveLength, bool leftTurnDisabled = false)
		{
			var random = new Random();
			var size = moves * 2 - 1;
			var pool = size > 1048576 ? null : ArrayPool<StepCount>.Shared;
			var steps = pool?.Rent(size) ?? new StepCount[size];

			for (var i = 0; i < size; i++)
			{
				switch (i % 2)
				{
					case 0:
						steps[i] = new StepCount(Step.Forward, random.Next(maxMoveLength) + 1);
						break;
					case 1:
						steps[i] = new StepCount(leftTurnDisabled ? Step.TurnRight : (random.Next(2) == 0 ? Step.TurnRight : Step.TurnLeft));
						break;
				}
			}

			var hash = steps.ToGenomeHash();
			pool?.Return(steps);
			yield return hash;
		}

		public readonly IReadOnlyList<Step> AvailableSteps;

		private int _generatedCount;
		public int GeneratedCount => _generatedCount;

		readonly LinkedList<Step> _lastGenerated = new LinkedList<Step>();
		readonly Random Randomizer = new Random();
		readonly bool LeftTurnDisabled;
		/*
         * The goal here is to produce unique eaters.
         */
		protected override Genome GenerateOneInternal()
		{
			lock (_lastGenerated)
			{
				var lastIndex = _lastGenerated.Count;
				if (lastIndex == 0)
				{
					_lastGenerated.AddLast(Step.Forward);
				}
				else
				{
					bool carried;
					var _node = _lastGenerated.Last;
					do
					{
						carried = false;

						// ReSharper disable once SwitchStatementMissingSomeCases
						switch (_node.Value)
						{
							case Step.Forward:
								_node.Value = LeftTurnDisabled ? Step.TurnRight : Step.TurnLeft;
								break;

							case Step.TurnLeft:
								_node.Value = Step.TurnRight;
								break;

							case Step.TurnRight:
								_node.Value = Step.Forward;
								_node = _node.Previous;
								if (_node == null)
								{
									_node = _lastGenerated.AddFirst(Step.Forward);
								}
								else
								{
									carried = true;
								}
								break;
						}

					}
					while (carried || _lastGenerated.HasConcecutiveTurns());
				}


				Interlocked.Increment(ref _generatedCount);
				return new Genome(_lastGenerated);
			}
		}

		protected override Genome[] CrossoverInternal(Genome a, Genome b)
		{
			var aLen = a.Genes.Length;
			var bLen = b.Genes.Length;
			if (aLen == 0 || bLen == 0 || aLen == 1 && bLen == 1) return null;

			var aPoint = Randomizer.Next(aLen - 1) + 1;
			var bPoint = Randomizer.Next(bLen - 1) + 1;

			var aGenes = a.Genes.ToArray();
			var bGenes = b.Genes.ToArray();

			return new[]
			{
				new Genome(aGenes.Take(aPoint).Concat(bGenes.Skip(bPoint))),
				new Genome(bGenes.Take(bPoint).Concat(aGenes.Skip(aPoint))),
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
				var asRemoved = Remove(genes, index, r).ToArray();
				var rlen = asRemoved.Length;
				var n = Randomizer.Next(rlen * 3); // 1 in 3 chances to 'swap' instead of remove.
				if (n == index) n++;
				return n < rlen
					? new Genome(Splice(asRemoved, n, genes.Skip(index).Take(r).ToArray()))
					: new Genome(asRemoved);
			}

			// Replace or insert...
			var g = AvailableSteps[i];
			Genome Insert(Step s, int count)
				=> new Genome(Splice(genes, index, s, count));

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
			return new Genome(genes);

		}

	}
}
