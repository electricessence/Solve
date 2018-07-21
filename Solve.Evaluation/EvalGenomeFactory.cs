using Open.Collections;
using Open.Collections.Synchronized;
using Open.Evaluation;
using Open.Evaluation.Arithmetic;
using Open.Evaluation.Catalogs;
using Open.Evaluation.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Solve.Evaluation
{
	using EvaluationRegistry = Registry;
	using IGene = IEvaluate<double>;

	// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
	public partial class EvalGenomeFactory<TGenome> : ReducibleGenomeFactoryBase<TGenome>
		where TGenome : EvalGenome

	{
		public EvalGenomeFactory(IEnumerable<TGenome> seeds = null) : base(seeds)
		{ }

		readonly EvaluationCatalog<double> Catalog = new EvaluationCatalog<double>();

		#region ParamOnly

		readonly LockSynchronizedHashSet<int> ParamsOnlyAttempted = new LockSynchronizedHashSet<int>();

		protected TGenome GenerateParamOnly(ushort id)
			=> Registration(Catalog.GetParameter(id));

		#endregion

		#region Operated

		static IEnumerable<ushort> UShortRange(ushort start, ushort max)
		{
			var s = start;
			while (s < max)
				yield return s++;
		}

		readonly ConcurrentDictionary<ushort, IEnumerator<TGenome>> OperatedCatalog =
			new ConcurrentDictionary<ushort, IEnumerator<TGenome>>();

		protected IEnumerable<TGenome> GenerateOperated(ushort paramCount = 2)
		{
			if (paramCount < 2)
				throw new ArgumentOutOfRangeException(nameof(paramCount), paramCount,
					"Must have at least 2 parameter count.");

			var operators = EvaluationRegistry.Arithmetic.Operators;

			return UShortRange(0, paramCount)
				.Combinations(paramCount)
				.SelectMany(combination =>
				{
					var children = combination.Select(p => Catalog.GetParameter(p)).ToArray();
					return operators.Select(op =>
						Registration(EvaluationRegistry.Arithmetic.GetOperator(Catalog, op, children)));
				});
		}

		#endregion

		#region Functions

		readonly ConcurrentDictionary<ushort, IEnumerator<TGenome>> FunctionedCatalog =
			new ConcurrentDictionary<ushort, IEnumerator<TGenome>>();

		protected IEnumerable<TGenome> GenerateFunctioned(ushort id)
		{
			var p = Catalog.GetParameter(id);
			foreach (var op in EvaluationRegistry.Arithmetic.Functions)
			{
				// ReSharper disable once SwitchStatementMissingSomeCases
				switch (op)
				{
					case Exponent.SYMBOL:
						yield return Registration(Catalog.GetExponent(p, -1));
						yield return Registration(Catalog.GetExponent(p, 0.5));
						break;
				}
			}
		}

		#endregion

		protected override TGenome GenerateOneInternal()
		{
			// ReSharper disable once NotAccessedVariable
			var attempts = 0; // For debugging.
			TGenome genome = null;

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

		protected virtual TGenome Create(IGene root) => (TGenome)new EvalGenome(root);

		protected TGenome Registration(IGene root, Action<TGenome> onBeforeAdd = null)
		{
			Debug.Assert(root != null);
			// ReSharper disable once ConditionIsAlwaysTrueOrFalse
			if (root == null) return null;
			Register(root.ToStringRepresentation(),
				() => Create(root), out var target,
				t =>
				{
					onBeforeAdd?.Invoke(t);
					t.Freeze();
				});
			return target;
		}

		protected override TGenome GetReducedInternal(TGenome source)
			=> source.Root is IReducibleEvaluation<IGene> root
			   && root.TryGetReduced(Catalog, out var reduced)
			   && reduced != root
				? Create(reduced)
				: null;

	}
}
