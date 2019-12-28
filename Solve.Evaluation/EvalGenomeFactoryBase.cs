using Open.Collections;
using Open.Collections.Synchronized;
using Open.Evaluation.Catalogs;
using Open.Evaluation.Core;
using Open.Hierarchy;
using Open.RandomizationExtensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Solve.Evaluation
{
	// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
	public abstract class EvalGenomeFactoryBase<T> : ReducibleGenomeFactoryBase<EvalGenome<T>>
		where T : IComparable
	{
		protected EvalGenomeFactoryBase()
		{ }

		protected EvalGenomeFactoryBase(IEnumerable<EvalGenome<T>> seeds) : base(seeds)
		{ }

		public readonly EvaluationCatalog<T> Catalog = new EvaluationCatalog<T>();

		#region ParamOnly

		readonly LockSynchronizedHashSet<int> ParamsOnlyAttempted = new LockSynchronizedHashSet<int>();

		protected EvalGenome<T> GenerateParamOnly(ushort id)
			=> Registration(Catalog.GetParameter(id), "GenerateParamOnly");

		#endregion

		#region Operated

		protected static IEnumerable<ushort> UShortRange(ushort start, ushort max)
		{
			var s = start;
			while (s < max)
				yield return s++;
		}

		readonly ConcurrentDictionary<ushort, IEnumerator<EvalGenome<T>>> OperatedCatalog =
			new ConcurrentDictionary<ushort, IEnumerator<EvalGenome<T>>>();

		protected abstract IEnumerable<EvalGenome<T>> GenerateOperated(ushort paramCount = 2);

		#endregion

		#region Functions

		readonly ConcurrentDictionary<ushort, IEnumerator<EvalGenome<T>>> FunctionedCatalog =
			new ConcurrentDictionary<ushort, IEnumerator<EvalGenome<T>>>();

		protected abstract IEnumerable<EvalGenome<T>> GenerateFunctioned(ushort id);

		#endregion

		protected override EvalGenome<T>? GenerateOneInternal()
		{
			// ReSharper disable once NotAccessedVariable
			var attempts = 0; // For debugging.
			EvalGenome<T>? genome = null;

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
						attempts++;
						if (!AlreadyProduced(genome))
							return genome;
					}

					paramCount += 1; // Operators need at least 2 params to start.

					// Then try an operator based version.
					var pcOne = paramCount;
					var operated = OperatedCatalog.GetOrAdd(++pcOne, pc =>
					{
						var e = GenerateOperated(pc)?.GetEnumerator();
						Debug.Assert(e != null);
						return e;
					});
					if (operated.ConcurrentTryMoveNext(out genome))
					{
						Debug.Assert(genome != null);
						attempts++;
						if (!AlreadyProduced(genome)) // May be supurfulous.
							return genome;
					}

					pcOne = paramCount;
					var functioned = FunctionedCatalog.GetOrAdd(--pcOne, pc => GenerateFunctioned(pc).GetEnumerator());
					// ReSharper disable once InvertIf
					if (functioned.MoveNext())
					{
						genome = functioned.Current;
						Debug.Assert(genome != null);
						attempts++;
						if (!AlreadyProduced(genome)) // May be supurfulous.
							return genome;
					}

				} while (--tries != 0);

			}

			return genome;

		}


		protected EvalGenome<T> Create(IEvaluate<T> root, (string message, string? data) origin)
		{
#if DEBUG
			var g = new EvalGenome<T>(root);
			g.AddLogEntry("Origin", origin.message, origin.data);
			return g;
#else
			return new EvalGenome<T>(root);
#endif
		}

		[return: NotNullIfNotNull("root")]
		protected EvalGenome<T>? Registration(IEvaluate<T>? root, (string message, string? data) origin, Action<EvalGenome<T>>? onBeforeAdd = null)
		{
			Debug.Assert(root != null);
			// ReSharper disable once ConditionIsAlwaysTrueOrFalse
			// ReSharper disable once HeuristicUnreachableCode
			if (root == null) return null;
			Register(root.ToStringRepresentation(),
				() => Create(root, origin), out var target,
				t =>
				{
					onBeforeAdd?.Invoke(t);
					t.Freeze();
				});
			return target;
		}

		protected EvalGenome<T> Registration(IEvaluate<T> root, string origin,
			Action<EvalGenome<T>>? onBeforeAdd = null)
			=> Registration(root, (origin, null), onBeforeAdd);

		protected override EvalGenome<T>? GetReduced(EvalGenome<T> source)
		=> Catalog.TryGetReduced(source.Root, out var reduced)
			? Create(reduced, ("Reduction of", source.Hash))
			: null;


		protected abstract IEnumerable<(IEvaluate<T> Root, string Origin)> GetVariations(IEvaluate<T> source);

		protected override IEnumerable<EvalGenome<T>> GetVariationsInternal(EvalGenome<T> source)
			=> GetVariations(source.Root)
				.Where(v => v.Root != null)
				.GroupBy(v => v.Root)
				.Select(g =>
				{
#if DEBUG
					return Create(g.Key,
						($"GetVariations:\n[{string.Join(", ", g.Select(v => v.Origin).Distinct().ToArray())}]", source.Hash));
#else
					return Create(g.Key, (null, null));
#endif
				})
				.Concat(base.GetVariationsInternal(source)
						?? Enumerable.Empty<EvalGenome<T>>());


		private const string CROSSOVER_OF = "Crossover of";

		protected override EvalGenome<T>[] CrossoverInternal(EvalGenome<T> a, EvalGenome<T> b)
		{
#if DEBUG
			// Shouldn't happen.
			Debug.Assert(a != null);
			Debug.Assert(b != null);

			Debug.Assert(a != b);

			// Avoid inbreeding. :P
			var aRed = GetReduced(a);
			var bRed = GetReduced(b);
			Debug.Assert(aRed == null && bRed == null || aRed != bRed);
#endif

			var aRoot = Catalog.Factory.Map(a.Root);
			var bRoot = Catalog.Factory.Map(b.Root);
			// Descendants only?  Swapping a root node is equivalent to swapping the entire genome.
			var aGeneNodes = aRoot.GetDescendantsOfType().ToArray();
			var bGeneNodes = bRoot.GetDescendantsOfType().ToArray();
			var aLen = aGeneNodes.Length;
			var bLen = bGeneNodes.Length;
			if (aLen == 0 || bLen == 0 || aLen == 1 && bLen == 1) return Array.Empty<EvalGenome<T>>();

			// Crossover scheme 1:  Swap a node.
			while (aGeneNodes.Length != 0)
			{
				var ag = aGeneNodes.RandomSelectOne();
				var agS = ag.Value!.ToStringRepresentation();
				var others = bGeneNodes.Where(g => g.Value!.ToStringRepresentation() != agS).ToArray();
				if (others.Length != 0)
				{
					// Do the swap...
					var bg = others.RandomSelectOne();
					var bgParent = bg.Parent!;

					var placeholder = Catalog.Factory.GetBlankNode();
					bgParent.Replace(bg, placeholder);
					ag.Parent!.Replace(ag, bg);
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

			return Array.Empty<EvalGenome<T>>();
		}

	}
}
