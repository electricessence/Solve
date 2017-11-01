using Open.Collections;
using Open.Collections.Synchronized;
using Open.Evaluation;
using Open.Evaluation.Arithmetic;
using Open.Hierarchy;
using Open.Numeric;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using IFunction = Open.Evaluation.IFunction<double>;
using IGene = Open.Evaluation.IEvaluate<double>;
using IOperator = Open.Evaluation.IOperator<Open.Evaluation.IEvaluate<double>, double>;
using EvaluationRegistry = Open.Evaluation.Registry;

namespace BlackBoxFunction
{

    public class GenomeFactory : Solve.ReducibleGenomeFactoryBase<Genome>
	{
		Catalog<IGene> Catalog = new Catalog<IGene>();

        LockSynchronizedHashSet<int> ParamsOnlyAttempted = new LockSynchronizedHashSet<int>();
		protected Genome GenerateParamOnly(ushort id)
		{
			return Registration(new Genome(Catalog.GetParameter(id)));
		}

		static IEnumerable<ushort> UShortRange(ushort start, ushort max)
		{
			ushort s = start;
			while (s < max)
			{
				yield return s;
				s++;
			}
		}

		ConcurrentDictionary<ushort, IEnumerator<Genome>> OperatedCatalog = new ConcurrentDictionary<ushort, IEnumerator<Genome>>();
		protected IEnumerable<Genome> GenerateOperated(ushort paramCount = 2)
		{
			if (paramCount < 2)
				throw new ArgumentOutOfRangeException("paramCount", paramCount, "Must have at least 2 parameter count.");

			foreach (var combination in UShortRange(0, paramCount).Combinations(paramCount))
			{
				foreach (var op in EvaluationRegistry.Arithmetic.Operators)
				{
					var children = new List<IGene>();
					foreach (var p in combination)
						children.Add(Catalog.GetParameter(p));

					switch (op)
					{
						case Sum.SYMBOL:
							yield return Registration(new Genome(new Sum(children)));
							break;
						case Product.SYMBOL:
							yield return Registration(new Genome(new Product(children)));
							break;
					}
				}
			}
		}

		ConcurrentDictionary<ushort, IEnumerator<Genome>> FunctionedCatalog = new ConcurrentDictionary<ushort, IEnumerator<Genome>>();
		protected IEnumerable<Genome> GenerateFunctioned(ushort id)
		{
			var p = Catalog.GetParameter(id);
			foreach (var op in EvaluationRegistry.Arithmetic.Functions)
			{
				switch (op)
				{
					case Exponent.SYMBOL:
						yield return Registration(new Genome(new Exponent(p, -1)));
						yield return Registration(new Genome(new Exponent(p, 1 / 2)));
						break;
				}
			}
		}


		protected override Genome GenerateOneInternal()
		{
			var attempts = 0; // For debugging.
			Genome genome = null;
			string hash = null;

			for (byte m = 1; m < 26; m++) // The 26 effectively represents the max parameter depth.
			{

				// Establish a maximum.
				var tries = 10;
				ushort paramCount = 0;

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
					ushort pcOne;
					pcOne = paramCount;
					var operated = OperatedCatalog.GetOrAdd(++pcOne, pc => GenerateOperated(pc).GetEnumerator());
					if (operated.MoveNext())
					{
						genome = operated.Current;
						hash = genome.Hash;
						attempts++;
						if (RegisterProduction(genome)) // May be supurfulous.
							return genome;
					}

					pcOne = paramCount;
					var functioned = FunctionedCatalog.GetOrAdd(--pcOne, pc => GenerateFunctioned(pc).GetEnumerator());
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
						genome = Mutate(Registry[RegistryOrder.Snapshot().RandomSelectOne()].Value, m);
						hash = genome?.Hash;
						attempts++;
						if (hash != null && RegisterProduction(genome))
							return genome;
					}
					while (--t != 0);

				}
				while (--tries != 0);

			}

			return genome;

		}

		protected IEnumerable<Genome> GenerateVariationsUnfiltered(Genome source)
		{
			var sourceTree = Catalog.Factory.Map(source.Root);
			var sourceNodes = sourceTree.GetNodes().ToArray();
			var count = sourceNodes.Length;

			for (var i = 0; i < count; i++)
			{
				yield return VariationCatalog.RemoveGene(sourceTree, sourceNodes[i]);
			}


			// Strip down parameter levels to search for significance.
			Genome paramRemoved = source;
			while (true)
			{
				paramRemoved = paramRemoved.Clone();
				var root = paramRemoved.Root;
				var paramGroups = paramRemoved.Genes
					.OfType<Parameter>()
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
			 * 6) Removing a function.
			 */

			var genes = Catalog.Factory.Map(target.Root);

			while (genes.Any())
			{
				var gene = genes.GetNodes().ToArray().RandomSelectOne();
				var gv = gene.Value;
				if (gv is Constant)
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
				else if (gv is Parameter)
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
									.MutateParameter(target, (Parameter)gene);

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
				else if (gene is IOperator)
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
									.ChangeOperation(target, (IOperator)gene);
								break;

							// Apply a function
							case 3:
								// Reduce the pollution of functions...
								if (RandomUtilities.Random.Next(0, gene is IFunction ? 4 : 2) == 0)
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
									.AddParameter(target, (IOperator)gene);
								break;

							case 6:
								ng = MutationCatalog
									.BranchOperation(target, (IOperator)gene);
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

			var aRoot = Catalog.Factory.Map(a.Root);
			var bRoot = Catalog.Factory.Map(b.Root);
			var aGeneNodes = aRoot.GetNodes().ToArray();
			var bGeneNodes = bRoot.GetNodes().ToArray();
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
					var bg = others.RandomSelectOne();
					return new Genome[]
					{
						Registration(new Genome(aRoot.CloneReplaced(ag,bg).Value)),
						Registration(new Genome(bRoot.CloneReplaced(bg,ag).Value))
					};
				}
				aGeneNodes = aGeneNodes.Where(g => g != ag).ToArray();
			}

			return null;
		}

		protected IEnumerable<Genome> ExpandInternal(Genome genome)
		{
			var r = genome.AsReduced();
			Debug.Assert(r != null);
			if (r != null && r != genome)
			{
				var v = (Genome)r.NextVariation();
				if (v != null) yield return AssertFrozen(v.AsReduced());
				var m = (Genome)r.NextMutation();
				if (m != null) yield return AssertFrozen(m.AsReduced());
			}
		}

		public override IEnumerable<Genome> Expand(Genome genome, IEnumerable<Genome> others = null)
		{
			return base.Expand(genome, ExpandInternal(genome));
		}
	}
}