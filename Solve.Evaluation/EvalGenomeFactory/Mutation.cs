using Open.Evaluation.Catalogs;
using Open.Evaluation.Core;
using Open.Hierarchy;
using Open.Numeric;
using System.Diagnostics;
using System.Linq;

namespace Solve.Evaluation
{

	using IFunction = IFunction<double>;
	using IGene = IEvaluate<double>;
	using IOperator = IOperator<IEvaluate<double>, double>;

	public partial class EvalGenomeFactory<TGenome>
		where TGenome : EvalGenome
	{

		// Keep in mind that Mutation is more about structure than 'variations' of multiples and constants.
		private IGene MutateUnfrozen(TGenome target)
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
				var gene = genes.GetNodes().ToArray().RandomSelectOne() as Node<IGene>;
				Debug.Assert(gene != null);
				var gv = gene.Value;
				switch (gv)
				{
					case Constant _:
						switch (RandomUtilities.Random.Next(4))
						{
							case 0:
								return Catalog.Variation
									.ApplyFunction(gene, Open.Evaluation.Registry.Arithmetic.Functions.RandomSelectOne());
							case 1:
								return Catalog.Mutation
									.MutateSign(gene, 1);
							default:
								return Catalog.RemoveNode(gene).Value;
						}

					case Parameter _:
						{
							var options = Enumerable.Range(0, 5).ToList();
							while (options.Any())
							{
								switch (options.RandomPluck())
								{
									case 0:
										return Catalog.Mutation
											.MutateSign(gene, 1);

									// Simply change parameters
									case 1:
										return Catalog.Mutation
											.MutateParameter(gene);

									// Apply a function
									case 2:
										// Reduce the pollution of functions...
										if (RandomUtilities.Random.Next(0, 2) == 0)
										{
											return Catalog.Variation
												.ApplyFunction(gene, Open.Evaluation.Registry.Arithmetic.Functions.RandomSelectOne());
										}

										break;

									// Split it...
									case 3:
										if (RandomUtilities.Random.Next(0, 3) == 0)
										{
											return Catalog.Mutation
												.Square(gene);
										}

										break;

									// Remove it!
									default:
										if (Catalog.Variation.TryRemoveValid(gene, out var attempt))
											return attempt;
										break;

								}
							}
						}
						break;
					default:
						if (gene.Value is IOperator opGene)
						{
							var options = Enumerable.Range(0, 8).ToList();
							while (options.Any())
							{
								IGene ng = null;
								switch (options.RandomPluck())
								{
									case 0:
										ng = Catalog.Mutation.MutateSign(gene, 1);
										break;

									case 1:
										ng = Catalog.Variation.PromoteChildren(gene);
										break;

									case 2:
										ng = Catalog.Mutation.ChangeOperation(target, opGene);
										break;

									// Apply a function
									case 3:
										// Reduce the pollution of functions...
										if (RandomUtilities.Random.Next(0, gv is IFunction ? 4 : 2) == 0)
										{
											var f = Open.Evaluation.Registry.Arithmetic.Functions.RandomSelectOne();
											// Function of function? Reduce probability even further. Coin toss.
											if (f.GetType() != gene.GetType() || RandomUtilities.Random.Next(2) == 0)
												return Catalog.Variation
													.ApplyFunction(gene, f);

										}
										break;

									case 4:
										Catalog.Variation.TryRemoveValid(gene, out ng);
										break;

									case 5:
										ng = Catalog.Mutation
											.AddParameter(target, opGene);
										break;

									case 6:
										ng = Catalog.Mutation
											.BranchOperation(target, opGene);
										break;

									case 7:
										// This has a potential to really bloat the function so allow, but very sparingly.
										if (RandomUtilities.Random.Next(0, 3) == 0)
										{
											return Catalog.Mutation
												.Square(gene);
										}
										break;
								}

								if (ng != null)
									return ng;
							}
						}

						break;
				}

			}

			return null;

		}

		protected override TGenome MutateInternal(TGenome target)
			=> Registration(MutateUnfrozen(target));
	}
}
