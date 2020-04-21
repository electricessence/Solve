using App.Metrics.Counter;
using Open.Collections;
using Open.Evaluation;
using Open.Evaluation.Arithmetic;
using Open.Evaluation.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Solve.Evaluation
{
	using EvaluationRegistry = Registry;

	// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
	public partial class NumericEvalGenomeFactory : EvalGenomeFactoryBase<double>
	{
		public NumericEvalGenomeFactory(IProvideCounterMetrics metrics)
			: base(metrics) { }

		public NumericEvalGenomeFactory(IProvideCounterMetrics metrics, IEnumerable<EvalGenome<double>> seeds)
			: base(metrics, seeds) { }

		public NumericEvalGenomeFactory(IProvideCounterMetrics metrics, params EvalGenome<double>[] seeds)
			: base(metrics, seeds) { }

		public NumericEvalGenomeFactory(IProvideCounterMetrics metrics, IEnumerable<string> seeds)
			: base(metrics)
		{
			InjectSeeds(seeds);
		}

		public NumericEvalGenomeFactory(IProvideCounterMetrics metrics, params string[] seeds)
			: this(metrics, (IEnumerable<string>)seeds)
		{
		}

		protected void InjectSeeds(IEnumerable<string> seeds)
			=> InjectSeeds(seeds?.Select(s => Create(Catalog.Parse(s), ("Seed", null))));


		#region Operated
		protected override IEnumerable<EvalGenome<double>> GenerateOperated(ushort paramCount = 2)
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
						Registration(
							EvaluationRegistry.Arithmetic.GetOperator(Catalog, op, children),
							$"EvalGenomeFactory.GenerateOperated({paramCount})"));
				});
		}
		#endregion

		#region Functions
		protected override IEnumerable<EvalGenome<double>> GenerateFunctioned(ushort id)
		{
			var p = Catalog.GetParameter(id);
			foreach (var op in EvaluationRegistry.Arithmetic.Functions)
			{
				// ReSharper disable once SwitchStatementMissingSomeCases
				switch (op)
				{
					case Exponent.SYMBOL:
						yield return Registration(Catalog.GetExponent(p, -1), "GenerateFunctioned > Division by Parameter");
						yield return Registration(Catalog.GetExponent(p, 0.5), "GenerateFunctioned > Square Root of Parameter");
						break;
				}
			}
		}
		#endregion

	}
}
