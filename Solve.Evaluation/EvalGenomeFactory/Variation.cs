using Open.Evaluation.Catalogs;
using Open.Evaluation.Core;
using Open.Hierarchy;
using System.Collections.Generic;
using System.Linq;
using Open.Evaluation.Arithmetic;

namespace Solve.Evaluation
{
	using IGene = IEvaluate<double>;

	public partial class EvalGenomeFactory<TGenome>
		where TGenome : EvalGenome
	{
		public IEnumerable<(IGene Root, string Origin)> GetVariations(IGene source)
		{
			var sourceTree = Catalog.Factory.Map(source);
			var descendantNodes = sourceTree.GetDescendantsOfType().ToArray();
			var count = descendantNodes.Length;

			int i;
			// Remove genes one at a time.
			for (i = 0; i < count; i++)
				if (Catalog.Variation.TryRemoveValid(descendantNodes[i], out var pruned))
					yield return (pruned, "Remove descendant by index");

			// Strip down parameter levels to search for significance.
			var paramRemoved = sourceTree;
			while (true)
			{
				paramRemoved = paramRemoved.Clone();
				//var root = paramRemoved.Root;
				var paramGroups = paramRemoved.GetDescendantsOfType()
					.Where(n => n.Value is IParameter<double>)
					.GroupBy(n => ((IParameter<double>)n.Value).ID)
					.OrderByDescending(g => g.Key)
					.FirstOrDefault()?
					.Where(n => n.IsValidForRemoval())
					.ToArray();

				if (paramGroups == null || paramGroups.Length < 2)
					break;

				foreach (var p in paramGroups)
					p.Parent.Remove(p);

				yield return (
					Catalog.FixHierarchy(paramRemoved).Value,
					"Parameter elimination");
			}

			for (i = 0; i < count; i++)
				yield return (
					Catalog.MultiplyNode(descendantNodes[i], -1),
					"Invert descenant sign");

			for (i = 0; i < count; i++)
				yield return (
					Catalog.AdjustNodeMultiple(descendantNodes[i], -1),
					"Reduce descendant multiple");

			for (i = 0; i < count; i++)
			{
				yield return (
					Catalog.Variation.PromoteChildren(descendantNodes[i]),
					"Promote descendant children");

				// Let mutation take care of this...
				// foreach (var fn in Operators.Available.Functions)
				// {
				//		yield return VariationCatalog.ApplyFunction(source, i, fn);
				// }
			}

			for (i = 0; i < count; i++)
				yield return (
					Catalog.AdjustNodeMultiple(descendantNodes[i], +1),
					"Increase descendant multiple");

			for (i = 0; i < count; i++)
				yield return (
					Catalog.AddConstant(descendantNodes[i], 2),
					"Add constant to descendant"); // 2 ensures the constant isn't negated when adding to a product.

			var reduced = Catalog.GetReduced(sourceTree.Value);

			yield return (reduced, "Reduction");

			if (reduced is Sum<double> sum
				&& sum.TryExtractGreatestFactor(Catalog, out var extracted, out _))
				yield return (extracted, "GCF Extracted Reduction");
		}

		protected override IEnumerable<TGenome> GetVariationsInternal(TGenome source)
			=> GetVariations(source.Root)
				.Where(v => v.Root != null)
				.GroupBy(v => v.Root)
				.Select(g =>
				{
#if DEBUG
					return Create(g.Key,
						($"EvalGenomeFactory.GetVariations:\n[{string.Join(", ", g.Select(v => v.Origin).Distinct().ToArray())}]", source.Hash));
#else
					return Create(g.Key, (null, null));
#endif
				})
				.Concat(base.GetVariationsInternal(source)
						?? Enumerable.Empty<TGenome>());


	}
}
