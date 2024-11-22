using Open.Evaluation.Arithmetic;
using Open.Evaluation.Catalogs;
using Open.Evaluation.Core;
using Open.Hierarchy;
using System.Diagnostics;

using IGene = Open.Evaluation.Core.IEvaluate<double>;

namespace Solve.Evaluation;
public partial class NumericEvalGenomeFactory
{
	protected override IEnumerable<(IGene Root, string Origin)> GetVariations(IGene source)
	{
		IGene? productOfSums = Catalog.Variation.FlattenProductofSums(source);

		if (productOfSums != source)
		{
			// Ensure not new instance.
			Debug.Assert(productOfSums.ToStringRepresentation() != source.ToStringRepresentation());
			yield return (productOfSums, "Product of Sums");
		}
		else
		{
			productOfSums = null;
		}

		if (Catalog.TryGetReduced(source, out IGene? reduced))
		{
			yield return (reduced, "Reduction");
			IGene reducedProductOfSums = Catalog.Variation.FlattenProductofSums(reduced);
			if (reducedProductOfSums != reduced && reducedProductOfSums != productOfSums)
			{
				// Ensure not new instance.
				Debug.Assert(reducedProductOfSums.ToStringRepresentation() != reduced.ToStringRepresentation());
				yield return (reducedProductOfSums, "Reduction product of Sums");
			}
		}
		else
		{
			reduced = source;
		}

		foreach (char op in Open.Evaluation.Registry.Arithmetic.Functions)
			yield return (Open.Evaluation.Registry.Arithmetic.GetFunction(Catalog, op, reduced), $"Root function ({op})");

		Node<IGene> sourceTree = Catalog.Factory.Map(source);
		Node<IGene>[] descendantNodes = sourceTree.GetDescendantsOfType().ToArray();
		int count = descendantNodes.Length;

		int i;
		// Remove genes one at a time.
		for (i = 0; i < count; i++)
		{
			if (Catalog.Variation.TryRemoveValid(descendantNodes[i], out IGene? pruned))
				yield return (pruned, "Remove descendant by index");
		}

		// Strip down parameter levels to search for significance.
		Node<IGene> paramRemoved = sourceTree;
		while (true)
		{
			paramRemoved = paramRemoved.Clone();
			//var root = paramRemoved.Root;
			Node<IGene>[]? paramGroups = paramRemoved.GetDescendantsOfType()
				.Where(n => n.Value is IParameter<double>)
				.GroupBy(n => ((IParameter<double>)n.Value!).ID)
				.OrderByDescending(g => g.Key)
				.FirstOrDefault()?
				.Where(n => n.IsValidForRemoval())
				.ToArray();

			if (paramGroups is null || paramGroups.Length < 2)
				break;

			foreach (Node<IGene>? p in paramGroups)
				p.Parent!.Remove(p);

			yield return (
				Catalog.FixHierarchy(paramRemoved).Recycle()!,
				"Parameter elimination");
		}

		for (i = 0; i < count; i++)
		{
			yield return (
				Catalog.MultiplyNode(descendantNodes[i], -1),
				"Invert descenant sign");
		}

		for (i = 0; i < count; i++)
		{
			yield return (
				Catalog.AdjustNodeMultiple(descendantNodes[i], -1),
				"Reduce descendant multiple");
		}

		for (i = 0; i < count; i++)
		{
			IGene? n = Catalog.Variation.PromoteChildren(descendantNodes[i]);
			if (n is null) continue;
			yield return (n, "Promote descendant children");

			// Let mutation take care of this...
			// foreach (var fn in Operators.Available.Functions)
			// {
			//		yield return VariationCatalog.ApplyFunction(source, i, fn);
			// }
		}

		if (source is IParent)
		{
			IGene paramExpIncrease = Catalog.Variation.IncreaseParameterExponents(source);
			if (paramExpIncrease != source)
			{
				yield return (paramExpIncrease,
					"Increase all parameter exponent");
			}
		}

		for (i = 0; i < count; i++)
		{
			yield return (
				Catalog.AdjustNodeMultiple(descendantNodes[i], -1),
				"Decrease descendant multiple");
		}

		for (i = 0; i < count; i++)
		{
			yield return (
				Catalog.AdjustNodeMultiple(descendantNodes[i], +1),
				"Increase descendant multiple");
		}

		for (i = 0; i < count; i++)
		{
			IGene? n = Catalog.TryAddConstant(descendantNodes[i], 2);
			if (n is null) continue;
			yield return (n, "Add constant to descendant"); // 2 ensures the constant isn't negated when adding to a product.
		}

		sourceTree.Recycle();
		if (reduced is not Sum<double> sum) yield break;

		sourceTree = Catalog.Factory.Map(reduced);

		Node<IGene>[] clonedChildren = sourceTree.Children.Where(c => c.Value is IConstant<double>).ToArray();
		if (sourceTree.Count > clonedChildren.Length)
		{
			foreach (Node<IGene>? c in clonedChildren)
				c.Detatch();
		}

		IGene? next = Catalog.FixHierarchy(sourceTree).Recycle()!;
		Debug.Assert(next is not null);
		sourceTree.Recycle();
		yield return (Catalog.GetReduced(next), "Constants Stripped");

		if (sum.TryExtractGreatestFactor(Catalog, out IGene? extracted, out _))
			yield return (extracted, "GCF Extracted Reduction");
	}
}
