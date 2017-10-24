using Open.Numeric;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Eater
{

	public class EaterFactory : Solve.ReducibleGenomeFactoryBase<EaterGenome>
	{
		int _generatedCount = 0;
		public int GeneratedCount
		{
			get { return _generatedCount; }
		}

		LinkedList<Step> _lastGenerated = new LinkedList<Step>();
		Random Randomizer = new Random();

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
								_node.Value = Step.TurnLeft;
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
				return new EaterGenome(_lastGenerated);
			}
		}

		protected override EaterGenome[] CrossoverInternal(EaterGenome a, EaterGenome b)
		{
			var aGenes = a.Genes;
			var bGenes = b.Genes;
			var aLen = aGenes.Length;
			var bLen = bGenes.Length;
			if (aLen == 0 || bLen == 0 || aLen == 1 && bLen == 1) return null;

			var aPoint = Randomizer.Next(aLen - 1) + 1;
			var bPoint = Randomizer.Next(bLen - 1) + 1;

			return new EaterGenome[]
			{
				new EaterGenome(aGenes.Take(aPoint).Concat(bGenes.Skip(bPoint))),
				new EaterGenome(bGenes.Take(bPoint).Concat(aGenes.Skip(aPoint))),
			};
		}

		protected override EaterGenome MutateInternal(EaterGenome target)
		{
			var genes = target.Genes;

			// 1 in 6 chance to append to start. (grow)
			if (Randomizer.Next(6) == 0) return new EaterGenome(Enumerable.Repeat(Step.Forward, 1).Concat(genes));

			var index = Randomizer.Next(genes.Length);
			var value = genes[index];

			// 1 in 4 chance to remove instead of alter.
			if (Randomizer.Next(4) == 0) return new EaterGenome(genes.Take(index).Concat(genes.Skip(index + 1)));

			var g = Steps.ALL.Where(s => s != value).RandomSelectOne();

			// 1 in 3 chance to 'splice' instead of modify.
			if (index!=0 && Randomizer.Next(3) == 0)
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

		public override IEnumerable<EaterGenome> Expand(EaterGenome genome)
		{
			var genes = genome.AsReduced().Genes;
			var len = genes.Length - 1;
			if (len > 1)
			{
				yield return new EaterGenome(genes.Take(len));
			}
			foreach (var g in base.Expand(genome))
				yield return g;
		}

	}
}