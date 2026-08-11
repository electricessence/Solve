using App.Metrics.Counter;
using Open.Collections;
using Open.Evaluation.Boolean;
using Open.Evaluation.Core;

using EvaluationRegistry = Open.Evaluation.Registry;

namespace Solve.Evaluation;

public partial class BooleanEvalGenomeFactory(IProvideCounterMetrics metrics) : EvalGenomeFactoryBase<bool>(metrics)
{
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
		{
			throw new ArgumentOutOfRangeException(nameof(paramCount), paramCount,
				"Must have at least 2 parameter count.");
		}

		System.Collections.Immutable.ImmutableArray<char> operators = EvaluationRegistry.Boolean.Operators;

		return UShortRange(0, paramCount)
			.Combinations(paramCount)
			.SelectMany(combination =>
			{
				Parameter<bool>[] children = combination.Select(p => Catalog.GetParameter(p)).ToArray();
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
		Parameter<bool> p = Catalog.GetParameter(id);
		// Not is the only unary boolean function; Conditional ('?') requires multiple children.
		yield return Registration(Catalog.Not(p), "GenerateFunctioned > Not");
	}
	#endregion

}
