using System;
using System.Collections.Generic;
using System.Linq;

namespace Eater
{

	public class EaterFactory : Solve.ReducibleGenomeFactoryBase<EaterGenome>
	{
		public EaterFactory(IEnumerable<EaterGenome> seeds = null, bool leftTurnDisabled = false) : base(seeds)
		{
			LeftTurnDisabled = leftTurnDisabled;
		}

		public int GeneratedCount { get; private set; } = 0;

		readonly LinkedList<Step> _lastGenerated = new LinkedList<Step>();
		readonly Random Randomizer = new Random();
		readonly bool LeftTurnDisabled;
		/*
         * The goal here is to produce unique eaters.
         */
		protected override EaterGenome GenerateOneInternal()
		{
			lock (_lastGenerated)
			{
				int lastIndex = _lastGenerated.Count;
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


				GeneratedCount++;
				return new EaterGenome(_lastGenerated);
			}
		}

		protected override EaterGenome[] CrossoverInternal(EaterGenome a, EaterGenome b)
		{
			var aLen = a.Genes.Length;
			var bLen = b.Genes.Length;
			if (aLen == 0 || bLen == 0 || aLen == 1 && bLen == 1) return null;

			var aPoint = Randomizer.Next(aLen - 1) + 1;
			var bPoint = Randomizer.Next(bLen - 1) + 1;

			var aGenes = a.Genes.ToArray();
			var bGenes = b.Genes.ToArray();

			return new EaterGenome[]
			{
				new EaterGenome(aGenes.Take(aPoint).Concat(bGenes.Skip(bPoint))),
				new EaterGenome(bGenes.Take(bPoint).Concat(aGenes.Skip(aPoint))),
			};
		}

		static IEnumerable<T> Remove<T>(int index, T[] source)
		{
			return source.Take(index).Concat(source.Skip(index + 1));
		}

		protected override EaterGenome MutateInternal(EaterGenome target)
		{
			var genes = target.Genes.ToArray();
			var index = Randomizer.Next(genes.Length);
			var value = genes[index];

			var stepCount = Steps.ALL.Count;
			if (LeftTurnDisabled) stepCount--;
			// 1 in 4 chance to remove instead of alter.
			var i = Randomizer.Next(genes.Length > 3 ? stepCount + 1 : stepCount);
			if (i == stepCount)
			{
				return new EaterGenome(Remove(index, genes));
			}

			var g = Steps.ALL[i];

			// 50/50 chance to 'splice' instead of modify.
			if (Randomizer.Next(2) == 0)
			{
				return new EaterGenome(
					genes.Take(index)
						.Concat(Enumerable.Repeat(g, 1))
						.Concat(genes.Skip(index)));
			}
			else
			{
				genes[index] = g;
				return new EaterGenome(genes);
			}
		}

	}
}
