using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BlackBoxFunction.Genes;
using Open;
using Open.Collections;
using Open.Threading;

namespace BlackBoxFunction
{

	public class GenomeFactory : GeneticAlgorithmPlatform.GenomeFactoryBase<Genome>
	{
		ConcurrentHashSet<int> ParamsOnlyAttempted = new ConcurrentHashSet<int>();
		protected Genome GenerateParamOnly(int id)
		{
			return Registration(new Genome(new ParameterGene(id)));
		}

		ConcurrentDictionary<int, IEnumerator<Genome>> OperatedCatalog = new ConcurrentDictionary<int, IEnumerator<Genome>>();
		protected IEnumerable<Genome> GenerateOperated(int paramCount = 2)
		{
			if (paramCount < 2)
				throw new ArgumentOutOfRangeException("paramCount", paramCount, "Must have at least 2 parameter count.");

			foreach (var combination in Enumerable.Range(0, paramCount).Combinations(paramCount))
			{
				foreach (var op in Operators.Available.Operators.Select(o => Operators.New(o)))
				{
					foreach (var p in combination)
						op.Add(new ParameterGene(p));
					yield return Registration(new Genome(op));
				}
			}
		}

		ConcurrentDictionary<int, IEnumerator<Genome>> FunctionedCatalog = new ConcurrentDictionary<int, IEnumerator<Genome>>();
		protected IEnumerable<Genome> GenerateFunctioned(int id)
		{
			foreach (var op in Operators.Available.Functions.Select(o => Operators.New(o)))
			{
				op.Add(new ParameterGene(id));
				yield return Registration(new Genome(op));
			}
		}


		protected Genome GenerateOneInternal(Genome[] source)
		{
			var attempts = 0; // For debugging.
			Genome genome = null;
			string hash = null;

			// Note: for now, we will only mutate by 1.

			// See if it's possible to mutate from the provided genomes.
			if (source != null && source.Length != 0)
			{
				AttemptNewMutation(source, out genome);
			}

			if (genome == null)
			{
				for (byte m = 1; m < 26; m++) // The 26 effectively represents the max parameter depth.
				{

					// Establish a maximum.
					var tries = 10;
					int paramCount = 0;

					do
					{
						if (ParamsOnlyAttempted.Add(paramCount))
						{
							// Try a param only version first.
							genome = GenerateParamOnly(paramCount);
							hash = genome.Hash;
							attempts++;
							if (RegisterProduction(genome)) // May be supurfulous.
								return genome;
						}

						paramCount += 1; // Operators need at least 2 params to start.

						// Then try an operator based version.
						var operated = OperatedCatalog.GetOrAdd(paramCount + 1, pc => GenerateOperated(pc).GetEnumerator());
						if (operated.MoveNext())
						{
							genome = operated.Current;
							hash = genome.Hash;
							attempts++;
							if (RegisterProduction(genome)) // May be supurfulous.
								return genome;
						}

						var functioned = FunctionedCatalog.GetOrAdd(paramCount - 1, pc => GenerateFunctioned(pc).GetEnumerator());
						if (functioned.MoveNext())
						{
							genome = functioned.Current;
							hash = genome.Hash;
							attempts++;
							if (RegisterProduction(genome)) // May be supurfulous.
								return genome;
						}


						var t = Math.Min(Registry.Count * 2, 100); // A local maximum.
						do
						{
							// NOTE: Let's use expansions here...
							genome = Mutate(Registry[RegistryOrder.Source.RandomSelectOne()].Value, m);
							hash = genome?.Hash;
							attempts++;
							if (hash != null && RegisterProduction(genome))
								return genome;
						}
						while (--t != 0);

					}
					while (--tries != 0);
				}
			}

			return genome;

		}
		public override Genome GenerateOne(Genome[] source = null)
		{
			Genome genome;
			using (TimeoutHandler.New(5000, ms =>
			{
				Console.WriteLine("Warning: {0}.GenerateOneInternal() is taking longer than {1} milliseconds.\n", this, ms);
			}))
			{
				genome = GenerateOneInternal(source);
			}

			genome = Registration(genome);

			Debug.Assert(genome != null, "Converged? No solutions? Saturated?");
			// if(genome==null)
			// 	throw "Failed... Converged? No solutions? Saturated?";

			return genome;

		}

		public static Genome ApplyClone(Genome source, int geneIndex, Action<IGene, Genome> handler)
		{
			if (geneIndex == -1)
				throw new ArgumentOutOfRangeException("geneIndex", "Can't be -1.");
			var newGenome = source.Clone();
			var gene = newGenome.Genes[geneIndex];
			handler(gene, newGenome);
			return newGenome;
		}

		public static Genome ApplyClone(Genome source, int geneIndex, Action<IGene> handler)
		{
			if (geneIndex == -1)
				throw new ArgumentOutOfRangeException("geneIndex", "Can't be -1.");
			var newGenome = source.Clone();
			var gene = newGenome.Genes[geneIndex];
			handler(gene);
			return newGenome;
		}

		public static Genome ApplyClone(Genome source, IGene gene, Action<IGene, Genome> handler)
		{
			return ApplyClone(source, source.Genes.IndexOf(gene), handler);
		}

		public static Genome ApplyClone(Genome source, IGene gene, Action<IGene> handler)
		{
			return ApplyClone(source, source.Genes.IndexOf(gene), handler);
		}

		public static class VariationCatalog
		{


			public static Genome AddConstant(Genome source, int geneIndex)
			{
				var gene = source.Genes[geneIndex] as SumGene;
				// Basically, as a helper, if we find a sum gene that doesn't have any constants, then ok, add '1'.
				return gene != null && !gene.Children.OfType<ConstantGene>().Any()
					? ApplyClone(source, geneIndex, g => ((SumGene)g).Add(new ConstantGene(1)))
					: null;
			}

			public static Genome IncreaseMultipleMagnitude(Genome source, int geneIndex)
			{
				return ApplyClone(source, geneIndex, g =>
				{
					var absMultiple = Math.Abs(g.Modifier);
					g.Modifier += g.Modifier / absMultiple;
				});
			}

			public static Genome ReduceMultipleMagnitude(Genome source, int geneIndex)
			{
				return ApplyClone(source, geneIndex, g =>
				{
					var absMultiple = Math.Abs(g.Modifier);
					g.Modifier -= g.Modifier / absMultiple;
				});
			}

			public static bool CheckRemovalValidity(Genome source, IGene gene)
			{
				if (gene == source.Root) return false;
				// Validate worthyness.
				var parent = source.FindParent(gene);

				// Search for potential futility...
				// Basically, if there is no dynamic genes left after reduction then it's not worth removing.
				if (/*parent != source.Root && */!parent.Where(g => g != gene && !(g is ConstantGene)).Any())
				{
					return CheckRemovalValidity(source, parent);
				}

				return false;
			}

			public static Genome RemoveGene(Genome source, int geneIndex)
			{
				// Validate worthyness.
				var gene = source.Genes[geneIndex];

				if (CheckRemovalValidity(source, gene))
				{
					return ApplyClone(source, geneIndex, (g, newGenome) =>
					{
						var parent = newGenome.FindParent(g);
						parent.Remove(g);
					});
				}
				return null;

			}
			public static Genome RemoveGene(Genome source, IGene gene)
			{
				return RemoveGene(source, source.Genes.IndexOf(gene));
			}

			public static bool CheckPromoteChildrenValidity(Genome source, IGene gene)
			{
				// Validate worthyness.
				var op = gene as GeneNode;
				return op != null && op.Count == 1;
			}

			// This should handle the case of demoting a function.
			public static Genome PromoteChildren(Genome source, int geneIndex)
			{
				// Validate worthyness.
				var gene = source.Genes[geneIndex];

				if (CheckPromoteChildrenValidity(source, gene))
				{
					return ApplyClone(source, geneIndex, (g, newGenome) =>
					{
						var op = (GeneNode)g;
						var child = op.Children.Single();
						op.Remove(child);
						newGenome.Replace(g, child);
					});
				}
				return null;

			}

			public static Genome PromoteChildren(Genome source, IGene gene)
			{
				return PromoteChildren(source, source.Genes.IndexOf(gene));
			}

			public static Genome ApplyFunction(Genome source, int geneIndex, char fn)
			{
				if (!Operators.Available.Functions.Contains(fn))
					throw new ArgumentException("Invalid function operator.", "fn");

				// Validate worthyness.
				return ApplyClone(source, geneIndex, (g, newGenome) =>
				{
					var newFn = Operators.New(fn);
					newGenome.Replace(g, newFn);
					newFn.Add(g);
				});

			}

			public static Genome ApplyFunction(Genome source, IGene gene, char fn)
			{
				return ApplyFunction(source, source.Genes.IndexOf(gene), fn);
			}
		}

		public static class MutationCatalog
		{
			public static Genome MutateSign(Genome source, IGene gene, int options = 3)
			{
				var isRoot = source.Root == gene;
				var parentIsSquareRoot = source.FindParent(gene) is SquareRootGene;
				return ApplyClone(source, gene, g =>
				{
					switch (RandomUtilities.Random.Next(options))
					{
						case 0:
							// Alter Sign
							g.Modifier *= -1;
							if (parentIsSquareRoot)
							{
								// Sorry, not gonna mess with unreal (sqrt neg numbers yet).
								if (RandomUtilities.Random.Next(2) == 0)
									goto case 1;
								else
									goto case 2;
							}
							break;
						case 1:
							// Don't zero the root or make the internal multiple negative.
							if (isRoot && g.Modifier == +1 || parentIsSquareRoot && g.Modifier <= 0)
								goto case 2;
							// Decrease multiple.
							g.Modifier -= 1;
							break;
						case 2:
							// Don't zero the root. (makes no sense)
							if (isRoot && g.Modifier == -1)
								goto case 1;
							// Increase multiple.
							g.Modifier += 1;
							break;

					}
				});
			}

			public static Genome MutateParameter(Genome source, ParameterGene gene)
			{
				var inputParamCount = source.Genes.OfType<ParameterGene>().GroupBy(p => p.ToString()).Count();
				return ApplyClone(source, gene, (g, newGenome) =>
				{
					var parameter = (ParameterGene)g;
					var nextParameter = RandomUtilities.NextRandomIntegerExcluding(inputParamCount + 1, parameter.ID);
					newGenome.Replace(g, new ParameterGene(nextParameter, parameter.Modifier));
				});
			}

			public static Genome ChangeOperation(Genome source, OperatorGeneBase gene)
			{
				bool isFn = gene is FunctionGene;
				if (isFn)
				{
					// Functions with no other options?
					if (Operators.Available.Functions.Count < 2)
						return null;
				}
				else
				{
					// Never will happen, but logic states that this is needed.
					if (Operators.Available.Operators.Count < 2)
						return null;
				}

				return ApplyClone(source, gene, (g, newGenome) =>
				{
					var og = (OperatorGeneBase)g;
					OperatorGeneBase replacement = isFn
						? Operators.GetRandomFunctionGene(og.Operator)
						: Operators.GetRandomOperationGene(og.Operator);
					replacement.AddThese(og.Children);
					og.Clear();
					newGenome.Replace(g, replacement);
				});
			}

			public static Genome AddParameter(Genome source, OperatorGeneBase gene)
			{
				bool isFn = gene is FunctionGene;
				if (isFn)
				{
					// Functions with no other options?
					if (gene is SquareRootGene || gene is DivisionGene)
						return null;
				}

				var inputParamCount = source.Genes.OfType<ParameterGene>().GroupBy(p => p.ToString()).Count();
				return ApplyClone(source, gene, (g, newGenome) =>
				{
					var og = (OperatorGeneBase)g;
					og.Add(new ParameterGene(RandomUtilities.Random.Next(inputParamCount + 1)));
				});
			}

			public static Genome BranchOperation(Genome source, OperatorGeneBase gene)
			{
				var inputParamCount = source.Genes.OfType<ParameterGene>().GroupBy(p => p.ToString()).Count();
				return ApplyClone(source, gene, (g, newGenome) =>
				{
					var n = new ParameterGene(RandomUtilities.Random.Next(inputParamCount));
					var newOp = Operators.GetRandomOperationGene();

					if (gene is FunctionGene || RandomUtilities.Random.Next(4) == 0)
					{
						var index = RandomUtilities.Random.Next(2);
						if (index == 1)
						{
							newOp.Add(n);
							newOp.Add(g);
						}
						else
						{
							newOp.Add(g);
							newOp.Add(n);
						}
						newGenome.Replace(g, newOp);
					}
					else
					{
						newOp.Add(n);
						// Useless to divide a param by itself, avoid...
						newOp.Add(new ParameterGene(RandomUtilities.Random.Next(inputParamCount)));

						((OperatorGeneBase)g).Add(newOp);
					}

				});
			}

			public static Genome Square(Genome source, IGene gene)
			{
				if (source.FindParent(gene) is SquareRootGene)
					return null;

				return ApplyClone(source, gene, (g, newGenome) =>
				{
					var newFn = new ProductGene(g.Modifier);
					g.Modifier = 1;
					newFn.Add(g);
					newFn.Add(g.Clone());
					newGenome.Replace(g, newFn);
				});
			}

		}

		protected IEnumerable<Genome> GenerateVariationsUnfiltered(Genome source)
		{
			var sourceGenes = source.Genes;
			var count = sourceGenes.Length;

			for (var i = 0; i < count; i++)
			{
				yield return VariationCatalog.RemoveGene(source, i);
			}


			// Strip down parameter levels to search for significance.
			Genome paramRemoved = source;
			while (true)
			{
				paramRemoved = paramRemoved.Clone();
				var root = paramRemoved.Root;
				var paramGroups = paramRemoved.Genes
					.OfType<ParameterGene>()
					.Where(g => g != root)
					.GroupBy(g => g.ID)
					.OrderByDescending(g => g.Key)
					.FirstOrDefault()?.ToArray();

				if (paramGroups == null || paramGroups.Length < 2)
					break;

				foreach (var p in paramGroups)
				{
					var parent = paramRemoved.FindParent(p);
					parent.Remove(p);
				}

				yield return paramRemoved;
			}



			for (var i = 0; i < count; i++)
			{
				yield return VariationCatalog.RemoveGene(source, i);
			}

			for (var i = 0; i < count; i++)
			{
				yield return VariationCatalog.ReduceMultipleMagnitude(source, i);
			}

			for (var i = 0; i < count; i++)
			{
				yield return VariationCatalog.PromoteChildren(source, i);

				// Let mutation take care of this...
				// foreach (var fn in Operators.Available.Functions)
				// {
				// 	yield return VariationCatalog.ApplyFunction(source, i, fn);
				// }
			}

			for (var i = 0; i < count; i++)
			{
				yield return VariationCatalog.IncreaseMultipleMagnitude(source, i);
			}

			for (var i = 0; i < count; i++)
			{
				yield return VariationCatalog.AddConstant(source, i);
			}

		}
		protected IEnumerable<Genome> GenerateVariations(Genome source)
		{
			return GenerateVariationsUnfiltered(source)
				.Where(genome => genome != null)
				.Select(genome => Registration(genome.AsReduced()))
				.GroupBy(g => g.Hash)
				.Select(g => g.First());
		}

		protected override Genome Registration(Genome target)
		{
			if (target == null) return null;
			Register(target, out target, hash =>
			{
				Debug.Assert(target.Hash == hash);
				target.RegisterVariations(GenerateVariations(target));
				target.RegisterMutations(Mutate(target));

				var reduced = target.AsReduced();
				if (reduced != target)
				{
					// A little caution here. Some possible evil recursion?
					var reducedRegistration = Registration(reduced);
					if (reduced != reducedRegistration)
						target.ReplaceReduced(reducedRegistration);

					reduced.RegisterExpansion(hash);
				}
				target.Freeze();
			});
			return target;
		}

		// Keep in mind that Mutation is more about structure than 'variations' of multiples and constants.
		private Genome MutateUnfrozen(Genome target)
		{
			/* Possible mutations:
			 * 1) Adding a parameter node to an operation.
			 * 2) Apply a function to node.
			 * 3) Adding an operator and a parameter node.
			 * 4) Removing a node from an operation.
			 * 5) Removing an operation.
			 * 6) Removing a function. */

			var genes = target.Genes;

			while (genes.Any())
			{
				var gene = genes.RandomSelectOne();
				if (gene is ConstantGene)
				{
					switch (RandomUtilities.Random.Next(4))
					{
						case 0:
							return VariationCatalog
								.ApplyFunction(target, gene, Operators.GetRandomFunction());
						case 1:
							return MutationCatalog
								.MutateSign(target, gene, 1);
						default:
							return VariationCatalog
								.RemoveGene(target, gene);
					}

				}
				else if (gene is ParameterGene)
				{
					var options = Enumerable.Range(0, 5).ToList();
					while (options.Any())
					{
						switch (options.RandomPluck())
						{
							case 0:
								return MutationCatalog
									.MutateSign(target, gene, 1);

							// Simply change parameters
							case 1:
								return MutationCatalog
									.MutateParameter(target, (ParameterGene)gene);

							// Apply a function
							case 2:
								// Reduce the pollution of functions...
								if (RandomUtilities.Random.Next(0, 2) == 0)
								{
									return VariationCatalog
										.ApplyFunction(target, gene, Operators.GetRandomFunction());
								}
								break;

							// Split it...
							case 3:
								if (RandomUtilities.Random.Next(0, 3) == 0)
								{
									return MutationCatalog
											.Square(target, gene);
								}
								break;

							// Remove it!
							default:
								var attempt = VariationCatalog.RemoveGene(target, gene);
								if (attempt != null)
									return attempt;
								break;

						}
					}


				}
				else if (gene is OperatorGeneBase)
				{
					var options = Enumerable.Range(0, 8).ToList();
					while (options.Any())
					{
						Genome ng = null;
						switch (options.RandomPluck())
						{
							case 0:
								ng = MutationCatalog
									.MutateSign(target, gene, 1);
								break;

							case 1:
								ng = VariationCatalog
									.PromoteChildren(target, gene);
								break;

							case 2:
								ng = MutationCatalog
									.ChangeOperation(target, (OperatorGeneBase)gene);
								break;

							// Apply a function
							case 3:
								// Reduce the pollution of functions...
								if (RandomUtilities.Random.Next(0, gene is FunctionGene ? 4 : 2) == 0)
								{
									var f = Operators.GetRandomFunction();
									// Function of function? Reduce probability even further. Coin toss.
									if (f.GetType() != gene.GetType() || RandomUtilities.Random.Next(2) == 0)
										return VariationCatalog
										.ApplyFunction(target, gene, f);

								}
								break;

							case 4:
								ng = VariationCatalog.RemoveGene(target, gene);
								break;

							case 5:
								ng = MutationCatalog
									.AddParameter(target, (OperatorGeneBase)gene);
								break;

							case 6:
								ng = MutationCatalog
									.BranchOperation(target, (OperatorGeneBase)gene);
								break;

							case 7:
								// This has a potential to really bloat the function so allow, but very sparingly.
								if (RandomUtilities.Random.Next(0, 3) == 0)
								{
									return MutationCatalog
										.Square(target, gene);
								}
								break;
						}

						if (ng != null)
							return ng;
					}



				}

			}

			return null;

		}

		protected override Genome MutateInternal(Genome target)
		{
			return Registration(MutateUnfrozen(target));
		}

		protected override Genome[] CrossoverInternal(Genome a, Genome b)
		{
			if (a == null || b == null) return null;

			// Avoid inbreeding. :P
			if (a.AsReduced().Hash == b.AsReduced().Hash) return null;

			var aGenes = a.Genes;
			var bGenes = b.Genes;
			var aLen = aGenes.Length;
			var bLen = bGenes.Length;
			if (aLen == 0 || bLen == 0 || aLen == 1 && bLen == 1) return null;

			// Crossover scheme 1:  Swap a node.
			a = a.Clone();
			b = b.Clone();
			aGenes = a.Genes;
			bGenes = b.Genes;
			while (aGenes.Length != 0)
			{
				var ag = aGenes.RandomSelectOne();
				var agS = ag.ToString();
				var others = bGenes.Where(g => g.ToString() != agS).ToArray();
				if (others.Length != 0)
				{
					var bg = others.RandomSelectOne();
					a.Replace(ag, bg);
					b.Replace(bg, ag);
					return new Genome[] { Registration(a), Registration(b) };
				}
				aGenes = aGenes.Where(g => g != ag).ToArray();
			}

			return null;
		}


		public override IEnumerable<Genome> Expand(Genome genome)
		{
			// Calling AsReduced will cut down on unnecessary retries of existing formulas.
			var r = genome.AsReduced();
			Debug.Assert(r != null);
			if (r != null && r != genome)
			{
				yield return AssertFrozen(r);
				var v = (Genome)r.NextVariation();
				if (v != null) yield return AssertFrozen(v.AsReduced());
				var m = (Genome)r.NextMutation();
				if (m != null) yield return AssertFrozen(m.AsReduced());
			}
			foreach (var e in base.Expand(genome))
				yield return e; // Assertion happens in base.
		}
	}
}