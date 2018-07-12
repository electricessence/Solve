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

	public partial class EvalGenomeFactory : ReducibleGenomeFactoryBase<EvalGenome>
	{
		readonly EvaluationCatalog<double> Catalog = new EvaluationCatalog<double>();

		#region ParamOnly
		readonly LockSynchronizedHashSet<int> ParamsOnlyAttempted = new LockSynchronizedHashSet<int>();
		protected EvalGenome GenerateParamOnly(ushort id)
			=> Registration(Catalog.GetParameter(id));
		#endregion

		#region Operated
		static IEnumerable<ushort> UShortRange(ushort start, ushort max)
		{
			ushort s = start;
			while (s < max)
			{
				yield return s;
				s++;
			}
		}

		readonly ConcurrentDictionary<ushort, IEnumerator<EvalGenome>> OperatedCatalog = new ConcurrentDictionary<ushort, IEnumerator<EvalGenome>>();
		protected IEnumerable<EvalGenome> GenerateOperated(ushort paramCount = 2)
		{
			if (paramCount < 2)
				throw new ArgumentOutOfRangeException(nameof(paramCount), paramCount, "Must have at least 2 parameter count.");

			foreach (var combination in UShortRange(0, paramCount).Combinations(paramCount))
			{
				var children = combination.Select(p => Catalog.GetParameter(p)).ToArray();
				foreach (var op in EvaluationRegistry.Arithmetic.Operators)
				{
					yield return Registration(Catalog.GetOperator<double>(op, children));
				}
			}
		}
		#endregion

		#region Functions
		readonly ConcurrentDictionary<ushort, IEnumerator<EvalGenome>> FunctionedCatalog = new ConcurrentDictionary<ushort, IEnumerator<EvalGenome>>();
		protected IEnumerable<EvalGenome> GenerateFunctioned(ushort id)
		{
			var p = Catalog.GetParameter(id);
			foreach (var op in EvaluationRegistry.Arithmetic.Functions)
			{
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

		protected override EvalGenome GenerateOneInternal()
		{
			// ReSharper disable once NotAccessedVariable
			var attempts = 0; // For debugging.
			EvalGenome genome = null;

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
						if (RegisterProduction(genome)) // May be supurfulous.
							return genome;
					}

					paramCount += 1; // Operators need at least 2 params to start.

					// Then try an operator based version.
					var pcOne = paramCount;
					var operated = OperatedCatalog.GetOrAdd(++pcOne, pc => GenerateOperated(pc).GetEnumerator());
					if (operated.MoveNext())
					{
						genome = operated.Current;
						Debug.Assert(genome != null);
						attempts++;
						if (RegisterProduction(genome)) // May be supurfulous.
							return genome;
					}

					pcOne = paramCount;
					var functioned = FunctionedCatalog.GetOrAdd(--pcOne, pc => GenerateFunctioned(pc).GetEnumerator());
					if (functioned.MoveNext())
					{
						genome = functioned.Current;
						Debug.Assert(genome != null);
						attempts++;
						if (RegisterProduction(genome)) // May be supurfulous.
							return genome;
					}

					var t = Math.Min(Registry.Count * 2, 100); // A local maximum.
					do
					{
						// NOTE: Let's use expansions here...
						genome = Mutate(Registry[RegistryOrder.Snapshot().RandomSelectOne()].Value, m);
						var hash = genome?.Hash;
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


		protected EvalGenome Registration(IGene root)
		{
			if (root == null) return null;
			Register(root.ToStringRepresentation(),
				() => new EvalGenome(root),
				out var target);
			return target;
		}

		protected override EvalGenome GetReducedInternal(EvalGenome source)
			=> source.Root is IReducibleEvaluation<IGene> root
			   && root.TryGetReduced(Catalog, out var reduced)
			   && reduced != root
				? new EvalGenome(reduced)
				: null;

	}
}
