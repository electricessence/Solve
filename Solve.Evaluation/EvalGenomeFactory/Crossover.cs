using Open.Hierarchy;
using Open.Numeric;
using System.Diagnostics;
using System.Linq;
using IGene = Open.Evaluation.Core.IEvaluate<double>;

namespace Solve.Evaluation
{

	public partial class EvalGenomeFactory
	{
		protected override EvalGenome[] CrossoverInternal(EvalGenome a, EvalGenome b)
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
			var aGeneNodes = aRoot.GetNodesOfType<Node<IGene>, Node<IGene>>().ToArray();
			var bGeneNodes = bRoot.GetNodesOfType<Node<IGene>, Node<IGene>>().ToArray();
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

					var placeholder = Catalog.Factory.GetBlankNode();
					bg.Parent.Replace(bg, placeholder);
					ag.Parent.Replace(ag, bg);
					Catalog.Factory.Recycle(placeholder);

					return new[]
					{
						Registration(Catalog.FixHierarchy(aRoot).Value),
						Registration(Catalog.FixHierarchy(bRoot).Value)
					};
				}
				aGeneNodes = aGeneNodes.Where(g => g != ag).ToArray();
			}

			return null;
		}


	}
}
