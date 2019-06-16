using Open.Evaluation.Core;
using Open.Hierarchy;
using Open.Numeric;
using Open.RandomizationExtensions;
using System;
using System.Linq;

namespace Solve.Evaluation
{
	using IGene = IEvaluate<bool>;

	public partial class BooleanEvalGenomeFactory
	{

		// Keep in mind that Mutation is more about structure than 'variations' of multiples and constants.
		private (IGene Root, string Origin) MutateUnfrozen(EvalGenome<bool> target)
		{
			/* Possible mutations:
			 * 1) Adding a parameter node to an operation.
			 * 2) Apply a function to node.
			 * 3) Adding an operator and a parameter node.
			 * 4) Removing a node from an operation.
			 * 5) Removing an operation.
			 * 6) Removing a function.
			 */

			var genes = Catalog.Factory.Map(target.Root);

			while (genes.Any())
			{
				var gene = genes
					.GetNodes()
					.ToArray()
					.RandomSelectOne() as Node<IGene>
					?? throw new InvalidCastException("Expected a Node<IGene>.");

				var gv = gene.Value;
				switch (gv)
				{
					// ReSharper disable once RedundantEmptySwitchSection
					default:
						break;
				}

			}

			return (null, null);

		}

		protected override EvalGenome<bool> MutateInternal(EvalGenome<bool> target)
		{
			var (root, origin) = MutateUnfrozen(target);
			return root == null ? null : Registration(root, ($"Mutation > {origin}", target.Hash));
		}
	}
}
