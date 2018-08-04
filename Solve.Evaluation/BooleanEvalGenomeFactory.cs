using Open.Collections;
using Open.Evaluation;
using Open.Evaluation.Boolean;
using Open.Evaluation.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Solve.Evaluation
{
	using EvaluationRegistry = Registry;

	// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
	public partial class BooleanEvalGenomeFactory : EvalGenomeFactoryBase<bool>
	{
		//public BooleanEvalGenomeFactory() { }

		//public BooleanEvalGenomeFactory(params string[] seeds)
		//{
		//	InjectSeeds(seeds);
		//}

		//protected void InjectSeeds(IEnumerable<string> seeds)
		//	=> InjectSeeds(seeds?.Select(s => Create(Catalog.Parse(s), ("Seed", null))));

		//public BooleanEvalGenomeFactory(params EvalGenome<double>[] seeds) : base(seeds)
		//{ }

		//public BooleanEvalGenomeFactory(IEnumerable<EvalGenome<double>> seeds) : base(seeds)
		//{ }

		#region Operated
		protected override IEnumerable<EvalGenome<bool>> GenerateOperated(ushort paramCount = 2)
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
							EvaluationRegistry.Boolean.GetOperator(Catalog, op, children),
							$"EvalGenomeFactory.GenerateOperated({paramCount})"));
				});
		}
		#endregion

		#region Functions
		protected override IEnumerable<EvalGenome<bool>> GenerateFunctioned(ushort id)
		{
			var p = Catalog.GetParameter(id);
			foreach (var op in EvaluationRegistry.Arithmetic.Functions)
			{
				// ReSharper disable once SwitchStatementMissingSomeCases
				switch (op)
				{
					case Not.SYMBOL:
						yield return Registration(Catalog.Not(p), "GenerateFunctioned > Not");
						break;
				}
			}
		}
		#endregion

	}
}
