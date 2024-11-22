using Open.Hierarchy;
using Open.RandomizationExtensions;

using IGene = Open.Evaluation.Core.IEvaluate<bool>;

namespace Solve.Evaluation;
public partial class BooleanEvalGenomeFactory
{
	// Keep in mind that Mutation is more about structure than 'variations' of multiples and constants.
	private (IGene? Root, string? Origin) MutateUnfrozen(EvalGenome<bool> target)
	{
		/* Possible mutations:
		 * 1) Adding a parameter node to an operation.
		 * 2) Apply a function to node.
		 * 3) Adding an operator and a parameter node.
		 * 4) Removing a node from an operation.
		 * 5) Removing an operation.
		 * 6) Removing a function.
		 */

		Node<IGene> genes = Catalog.Factory.Map(target.Root);

		while (genes.Count != 0)
		{
			Node<IGene> gene = genes
				.GetNodes()
				.ToArray()
				.RandomSelectOne() as Node<IGene>
				?? throw new InvalidCastException("Expected a Node<IGene>.");

			IGene gv = gene.Value;
			switch (gv)
			{
				// ReSharper disable once RedundantEmptySwitchSection
				default:
					break;
			}
		}

		return (null, null);
	}

	protected override EvalGenome<bool>? MutateInternal(EvalGenome<bool> target)
	{
		(IGene? root, string? origin) = MutateUnfrozen(target);
		return root is null ? null : Registration(root, ($"Mutation > {origin}", target.Hash));
	}
}
