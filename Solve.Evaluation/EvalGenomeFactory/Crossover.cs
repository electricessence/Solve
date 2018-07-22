using Open.Hierarchy;
using Open.Numeric;
using System.Diagnostics;
using System.Linq;
using IGene = Open.Evaluation.Core.IEvaluate<double>;

namespace Solve.Evaluation
{

	public partial class EvalGenomeFactory<TGenome>
		where TGenome : EvalGenome
	{
		private const string CROSSOVER_OF = "Crossover of";

		protected override TGenome[] CrossoverInternal(TGenome a, TGenome b)
		{
#if DEBUG
			// Shouldn't happen.
			Debug.Assert(a != null);
			Debug.Assert(b != null);

			// Avoid inbreeding. :P
			Debug.Assert(GetReduced(a).Hash != GetReduced(b).Hash);
#endif

			var aRoot = Catalog.Factory.Map(a.Root);
			var bRoot = Catalog.Factory.Map(b.Root);
			// Descendants only?  Swapping a root node is equivalent to swapping the entire genome.
			var aGeneNodes = aRoot.GetDescendantsOfType().ToArray();
			var bGeneNodes = bRoot.GetDescendantsOfType().ToArray();
			var aLen = aGeneNodes.Length;
			var bLen = bGeneNodes.Length;
			if (aLen == 0 || bLen == 0 || aLen == 1 && bLen == 1) return null;

			// Crossover scheme 1:  Swap a node.
			while (aGeneNodes.Length != 0)
			{
				var ag = aGeneNodes.RandomSelectOne();
				var agS = ag.Value.ToStringRepresentation();
				var others = bGeneNodes.Where(g => g.Value.ToStringRepresentation() != agS).ToArray();
				if (others.Length != 0)
				{
					// Do the swap...
					var bg = others.RandomSelectOne();
					var bgParent = bg.Parent;

					var placeholder = Catalog.Factory.GetBlankNode();
					bgParent.Replace(bg, placeholder);
					ag.Parent.Replace(ag, bg);
					bgParent.Replace(placeholder, ag);
					placeholder.Recycle();

					var origin = (CROSSOVER_OF, $"{a.Hash}\n{b.Hash}");
					return new[]
					{
						Registration(Catalog.FixHierarchy(aRoot).Recycle(), origin),
						Registration(Catalog.FixHierarchy(bRoot).Recycle(), origin)
					};
				}
				aGeneNodes = aGeneNodes.Where(g => g != ag).ToArray();
			}

			return null;
		}


	}
}
