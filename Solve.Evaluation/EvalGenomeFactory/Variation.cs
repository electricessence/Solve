using Open.Evaluation.Catalogs;
using Open.Evaluation.Core;
using Open.Hierarchy;
using System.Collections.Generic;
using System.Linq;
using IGene = Open.Evaluation.Core.IEvaluate<double>;

namespace Solve.Evaluation
{

	public partial class EvalGenomeFactory<TGenome>
		where TGenome : EvalGenome
	{
		public IEnumerable<IGene> GetVariations(IGene source)
		{
			var sourceTree = Catalog.Factory.Map(source);
			var descendantNodes = sourceTree.GetDescendantsOfType().ToArray();
			var count = descendantNodes.Length;

			int i;
			// Remove genes one at a time.
			for (i = 0; i < count; i++)
				yield return Catalog.RemoveDescendantAt(sourceTree, i);

			// Strip down parameter levels to search for significance.
			var paramRemoved = sourceTree;
			while (true)
			{
				paramRemoved = Catalog.Factory.Clone(paramRemoved);
				//var root = paramRemoved.Root;
				var paramGroups = paramRemoved.GetDescendantsOfType()
					.Where(n => n.Value is IParameter<double>)
					.GroupBy(n => ((IParameter<double>)n.Value).ID)
					.OrderByDescending(g => g.Key)
					.FirstOrDefault()?.ToArray();

				if (paramGroups == null || paramGroups.Length < 2)
					break;

				foreach (var p in paramGroups)
					p.Parent.Remove(p);

				yield return Catalog.FixHierarchy(paramRemoved).Value;
			}


			for (i = 0; i < count; i++)
				yield return Catalog.AdjustNodeMultiple(descendantNodes[i], -1);

			for (i = 0; i < count; i++)
			{
				yield return Catalog.Variation.PromoteChildren(descendantNodes[i]);

				// Let mutation take care of this...
				// foreach (var fn in Operators.Available.Functions)
				// {
				// 	yield return VariationCatalog.ApplyFunction(source, i, fn);
				// }
			}

			for (i = 0; i < count; i++)
				yield return Catalog.AdjustNodeMultiple(descendantNodes[i], +1);

			for (i = 0; i < count; i++)
				yield return Catalog.AddConstant(descendantNodes[i], 2); // 2 ensures the constant isn't negated when adding to a product.

		}

		protected override IEnumerable<TGenome> GetVariationsInternal(TGenome source)
			=> GetVariations(source.Root)
				.Where(v => v != null)
				.Distinct()
				.Select(Create)
				.Concat(base.GetVariationsInternal(source) ?? Enumerable.Empty<TGenome>());

	}
}
