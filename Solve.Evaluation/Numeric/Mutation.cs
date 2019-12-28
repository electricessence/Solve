using Open.Evaluation.Arithmetic;
using Open.Evaluation.Catalogs;
using Open.Evaluation.Core;
using Open.Hierarchy;
using Open.RandomizationExtensions;
using System;
using System.Diagnostics;
using System.Linq;

namespace Solve.Evaluation
{
	using IFunction = IFunction<double>;
	using IGene = IEvaluate<double>;
	using IOperator = IOperator<IEvaluate<double>, double>;

	public partial class NumericEvalGenomeFactory
	{

		// Keep in mind that Mutation is more about structure than 'variations' of multiples and constants.
		private (IGene? Root, string? Origin) MutateUnfrozen(EvalGenome<double> target)
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
					case Constant<double> c:
						switch (Randomizer.Random.Next(10))
						{
							case 0:
								// This is a bit controversial since it can bloat constant values.
								return (Catalog.Variation.ApplyRandomFunction(gene),
										"Apply function to constant");
							case 3:
								return (Catalog.Mutation.MutateSign(gene, 1),
										"Mutate sign of constant");

							case 4:
							case 5:
								if (gene.Parent?.Value is Exponent<double> exponent && exponent.Power == gv)
									goto case 6;

								goto default;

							case 6:
								var a = Math.Abs(c.Value);
								return ((a > 0 && a < 1) // Look for fractional exponents and avoid.
									? Catalog.ApplyClone(gene, newNode => Catalog.GetConstant(c.Value > 0 ? 1 : -1))
									// should be rare (1/10) chance to increase multiple for non power of exponents.
									: Catalog.AdjustNodeMultiple(gene, c.Value < 0 ? -1 : +1),
									"Increase constant");

							default:
								if (gene.Parent?.Value is Exponent<double>)
									return (Catalog.AdjustNodeMultiple(gene, c.Value > 0 ? -1 : +1),
										"Decrease constant");

								if (Catalog.Variation.TryRemoveValid(gene, out var newRoot))
									return (newRoot,
										"Remove constant");

								break;
						}
						break;

					case Parameter _:
						{
							var options = Enumerable.Range(0, 5).ToList();
							while (options.Any())
							{
								switch (options.RandomPluck())
								{
									case 0:
										return (Catalog.Mutation.MutateSign(gene, 1),
												"Mutate sign");

									// Simply change parameters
									case 1:
										return (Catalog.Mutation.MutateParameter(gene),
												"Mutate parameter");

									// Apply a function
									case 2:
										return (Catalog.Variation.ApplyRandomFunction(gene),
											"Apply random function to paramter");

									// Favor squaring...
									case 3:
										return (Catalog.Mutation.Square(gene),
											"Apply random function to paramter");

									//// Split it...
									//case 3:
									//	if (Randomizer.Random.Next(0, 2) == 0)
									//		return (Catalog.Mutation.Square(gene),
									//			"Square parameter");

									//	break;

									// Remove it!
									default:
										if (Catalog.Variation.TryRemoveValid(gene, out var attempt))
											return (attempt,
												"Remove descendant");
										break;

								}
							}
						}
						break;

					default:
						if (gv is IFunction)
						{
							var options = Enumerable.Range(0, 8).ToList();
							while (options.Any())
							{
								(IGene? Root, string? Origin) ng = default;
								switch (options.RandomPluck())
								{
									case 0 when gv is IOperator:
										ng = (Catalog.Mutation.MutateSign(gene, 1),
											"Mutate sign of function");
										break;

									case 1 when gv is IOperator:
										ng = (Catalog.Variation.PromoteChildren(gene),
											"Promote decendant children of function");
										break;

									case 2 when gv is IOperator:
										ng = (Catalog.Mutation.ChangeOperation(gene),
											"Change operation");
										break;

									case 3:
										// Reduce the pollution of functions...
										if (Randomizer.Random.Next(0, gv is IOperator ? 2 : 4) == 0)
										{
											//var f = Open.Evaluation.Registry.Arithmetic.Functions.RandomSelectOne();
											//// Function of function? Reduce probability even further. Coin toss.
											//if (f.GetType() != gene.GetType() || Randomizer.Random.Next(2) == 0)
											var f = Open.Evaluation.Registry.Arithmetic.GetRandomFunction(Catalog, gv);
											Debug.Assert(f != null);
											return (f,
												"Apply function to function");
										}
										break;

									case 4:
										if (Catalog.Variation.TryRemoveValid(gene, out var n))
											ng = (n,
												"Remove decendant function");
										break;

									case 5:
										ng = (Catalog.Mutation.AddParameter(gene),
											"Add parameter to function");
										break;

									case 6:
										ng = (Catalog.Mutation.BranchOperation(gene),
											"Branch operation");
										break;

									case 7:
										// This has a potential to really bloat the function so allow, but very sparingly.
										if (Randomizer.Random.Next(0, 3) == 0)
											return (Catalog.Mutation.Square(gene),
												"Square function");
										break;
								}

								if (ng.Root != null)
									return ng;
							}
						}

						break;
				}

			}

			return (null, null);

		}

		protected override EvalGenome<double>? MutateInternal(EvalGenome<double> target)
		{
			var (root, origin) = MutateUnfrozen(target);
			return root == null ? null : Registration(root, ($"Mutation > {origin}", target.Hash));
		}
	}
}
