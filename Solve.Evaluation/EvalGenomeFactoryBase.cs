using App.Metrics.Counter;
using Open.Collections;
using Open.Collections.Synchronized;
using Open.Evaluation.Catalogs;
using Open.Evaluation.Core;
using Open.Hierarchy;
using Open.RandomizationExtensions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Solve.Evaluation;

// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public abstract class EvalGenomeFactoryBase<T> : ReducibleGenomeFactoryBase<EvalGenome<T>>
	where T : notnull, IComparable<T>, IComparable
{
	protected EvalGenomeFactoryBase(IProvideCounterMetrics metrics) : base(metrics)
	{ }

	protected EvalGenomeFactoryBase(IProvideCounterMetrics metrics, IEnumerable<EvalGenome<T>>? seeds) : base(metrics, seeds)
	{ }

	public readonly EvaluationCatalog<T> Catalog = new();

	protected override void OnDispose()
	{
		base.OnDispose();
		Catalog.Dispose();
	}

	#region ParamOnly

	private readonly LockSynchronizedHashSet<int> ParamsOnlyAttempted = [];

	protected EvalGenome<T> GenerateParamOnly(ushort id)
		=> Registration(Catalog.GetParameter(id), "GenerateParamOnly");

	#endregion

	#region Operated

	protected static IEnumerable<ushort> UShortRange(ushort start, ushort max)
	{
		ushort s = start;
		while (s < max)
			yield return s++;
	}

	private readonly ConcurrentDictionary<ushort, IEnumerator<EvalGenome<T>>> OperatedCatalog =
		new();

	protected abstract IEnumerable<EvalGenome<T>> GenerateOperated(ushort paramCount = 2);

	#endregion

	#region Functions

	private readonly ConcurrentDictionary<ushort, IEnumerator<EvalGenome<T>>> FunctionedCatalog =
		new();

	protected abstract IEnumerable<EvalGenome<T>> GenerateFunctioned(ushort id);

	#endregion

	protected override EvalGenome<T>? GenerateOneInternal()
	{
		// ReSharper disable once NotAccessedVariable
		int attempts = 0; // For debugging.
		EvalGenome<T>? genome = null;

		for (byte m = 1; m < 26; m++) // The 26 effectively represents the max parameter depth.
		{
			// Establish a maximum.
			int tries = 10;
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

				paramCount++; // Operators need at least 2 params to start.

				// Then try an operator based version.
				ushort pcOne = paramCount;
				IEnumerator<EvalGenome<T>> operated = OperatedCatalog.GetOrAdd(++pcOne, pc =>
				{
					IEnumerator<EvalGenome<T>>? e = GenerateOperated(pc)?.GetEnumerator();
					Debug.Assert(e is not null);
					return e;
				});
				if (operated.ConcurrentTryMoveNext(out genome))
				{
					Debug.Assert(genome is not null);
					attempts++;
					if (!AlreadyProduced(genome)) // May be supurfulous.
						return genome;
				}

				pcOne = paramCount;
				IEnumerator<EvalGenome<T>> functioned = FunctionedCatalog.GetOrAdd(--pcOne, pc => GenerateFunctioned(pc).GetEnumerator());
				// ReSharper disable once InvertIf
				if (functioned.MoveNext())
				{
					genome = functioned.Current;
					Debug.Assert(genome is not null);
					attempts++;
					if (!AlreadyProduced(genome)) // May be supurfulous.
						return genome;
				}
			} while (--tries != 0);
		}

		return genome;
	}

	[SuppressMessage("Roslynator", "RCS1163:Unused parameter")]
	protected EvalGenome<T> Create(IEvaluate<T> root, (string? message, string? data) origin)
	{
#if DEBUG
		(string? message, string? data) = origin;
		Debug.Assert(message is not null);
		var g = new EvalGenome<T>(root);
		g.AddLogEntry("Origin", message, data);
		return g;
#else
		return new EvalGenome<T>(root);
#endif
	}

	[return: NotNullIfNotNull(nameof(root))]
	protected EvalGenome<T>? Registration(IEvaluate<T>? root, (string message, string? data) origin, Action<EvalGenome<T>>? onBeforeAdd = null)
	{
		Debug.Assert(root is not null);

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA1508 // Avoid dead conditional code
		if (root is null) return null;
#pragma warning restore CA1508 // Avoid dead conditional code
#pragma warning restore IDE0079 // Remove unnecessary suppression
		Register(root.ToStringRepresentation(),
			() => Create(root, origin), out EvalGenome<T>? target,
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
		=> Catalog.TryGetReduced(source.Root, out IEvaluate<T>? reduced)
			? Create(reduced, ("Reduction of", source.Hash))
			: null;

	protected abstract IEnumerable<(IEvaluate<T> Root, string Origin)> GetVariations(IEvaluate<T> source);

	protected override IEnumerable<EvalGenome<T>> GetVariationsInternal(EvalGenome<T> source)
		=> GetVariations(source.Root)
			.Where(v => v.Root is not null)
			.GroupBy(v => v.Root)
			.Select(g =>
#if DEBUG
				Create(g.Key,
					($"GetVariations:\n[{string.Join(", ", g.Select(v => v.Origin).Distinct())}]", source.Hash))
#else
				Create(g.Key, (null, null))
#endif
			)
			.Concat(base.GetVariationsInternal(source)
					?? []);

	private const string CROSSOVER_OF = "Crossover of";

	protected override EvalGenome<T>[] CrossoverInternal(EvalGenome<T> a, EvalGenome<T> b)
	{
#if DEBUG
		// Shouldn't happen.
		Debug.Assert(a is not null);
		Debug.Assert(b is not null);

		Debug.Assert(a != b);

		// Avoid inbreeding. :P
		EvalGenome<T>? aRed = GetReduced(a);
		EvalGenome<T>? bRed = GetReduced(b);
		Debug.Assert(aRed is null || bRed is null || aRed != bRed);
#endif

		Node<IEvaluate<T>> aRoot = Catalog.Factory.Map(a.Root);
		Node<IEvaluate<T>> bRoot = Catalog.Factory.Map(b.Root);
		// Descendants only?  Swapping a root node is equivalent to swapping the entire genome.
		Node<IEvaluate<T>>[] aGeneNodes = aRoot.GetDescendantsOfType().ToArray();
		Node<IEvaluate<T>>[] bGeneNodes = bRoot.GetDescendantsOfType().ToArray();
		int aLen = aGeneNodes.Length;
		int bLen = bGeneNodes.Length;
		if (aLen == 0 || bLen == 0 || aLen == 1 && bLen == 1)
			return [];

		// Crossover scheme 1:  Swap a node.
		while (aGeneNodes.Length != 0)
		{
			Node<IEvaluate<T>> ag = aGeneNodes.RandomSelectOne();
			string agS = ag.Value!.ToStringRepresentation();
			Node<IEvaluate<T>>[] others = bGeneNodes.Where(g => g.Value!.ToStringRepresentation() != agS).ToArray();
			if (others.Length != 0)
			{
				// Do the swap...
				Node<IEvaluate<T>> bg = others.RandomSelectOne();
				Node<IEvaluate<T>> bgParent = bg.Parent!;

				Node<IEvaluate<T>> placeholder = Catalog.Factory.GetBlankNode();
				bgParent.Replace(bg, placeholder);
				ag.Parent!.Replace(ag, bg);
				bgParent.Replace(placeholder, ag);
				placeholder.Recycle();

				(string CROSSOVER_OF, string) origin = (CROSSOVER_OF, $"{a.Hash}\n{b.Hash}");
				return
				[
					Registration(Catalog.FixHierarchy(aRoot).Recycle(), origin)!,
					Registration(Catalog.FixHierarchy(bRoot).Recycle(), origin)!
				];
			}

			aGeneNodes = aGeneNodes.Where(g => g != ag).ToArray();
		}

		return [];
	}
}
