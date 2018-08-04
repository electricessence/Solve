using Open.Evaluation.Core;
using System.Collections.Generic;

namespace Solve.Evaluation
{
	using IGene = IEvaluate<bool>;

	public partial class BooleanEvalGenomeFactory
	{
		protected override IEnumerable<(IGene Root, string Origin)> GetVariations(IGene source)
		{
			//var sourceTree = Catalog.Factory.Map(source);
			//var descendantNodes = sourceTree.GetDescendantsOfType().ToArray();
			//var count = descendantNodes.Length;

			return null;
			//int i;
			//// Remove genes one at a time.
			//for (i = 0; i < count; i++)
			//	if (Catalog.Variation.TryRemoveValid(descendantNodes[i], out var pruned))
			//		yield return (pruned, "Remove descendant by index");

			//for (i = 0; i < count; i++)
			//{
			//	yield return (
			//		Catalog.Variation.PromoteChildren(descendantNodes[i]),
			//		"Promote descendant children");

			//	// Let mutation take care of this...
			//	// foreach (var fn in Operators.Available.Functions)
			//	// {
			//	//		yield return VariationCatalog.ApplyFunction(source, i, fn);
			//	// }
			//}

			//if (source is IParent)
			//{
			//	var paramExpIncrease = Catalog.Variation.IncreaseParameterExponents(source);
			//	if (paramExpIncrease != source)
			//	{
			//		yield return (paramExpIncrease,
			//			"Increase all parameter exponent");
			//	}
			//}

			//for (i = 0; i < count; i++)
			//	yield return (
			//		Catalog.AddConstant(descendantNodes[i], 2),
			//		"Add constant to descendant"); // 2 ensures the constant isn't negated when adding to a product.

			//sourceTree.Recycle();
			//if (!(reduced is Sum<double> sum)) yield break;

			//sourceTree = Catalog.Factory.Map(reduced);

			//var clonedChildren = sourceTree.Children.Where(c => c.Value is IConstant<double>).ToArray();
			//if (sourceTree.Count > clonedChildren.Length)
			//{
			//	foreach (var c in clonedChildren)
			//		c.Detatch();
			//}

			//var next = Catalog.FixHierarchy(sourceTree).Recycle();
			//sourceTree.Recycle();
			//yield return (Catalog.GetReduced(next), "Constants Stripped");

			//if (sum.TryExtractGreatestFactor(Catalog, out var extracted, out _))
			//	yield return (extracted, "GCF Extracted Reduction");

		}


	}
}
