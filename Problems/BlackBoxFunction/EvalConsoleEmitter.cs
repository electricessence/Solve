using Open.Evaluation.Core;
using Solve;
using Solve.Evaluation;
using Solve.Experiment.Console;
using System.Text;

namespace BlackBoxFunction
{
	public class EvalConsoleEmitter : ConsoleEmitterBase<EvalGenome>
	{
		readonly ICatalog<IEvaluate<double>> Catalog;

		public EvalConsoleEmitter(ICatalog<IEvaluate<double>> catalog, uint sampleMinimum = 50)
			: base(sampleMinimum)
		{
			Catalog = catalog;
		}

		public EvalConsoleEmitter(EvalGenomeFactory<EvalGenome> factory, uint sampleMinimum = 50)
			: this(factory.Catalog, sampleMinimum)
		{

		}

		protected override void OnEmittingGenome(IProblem<EvalGenome> p, EvalGenome genome, Fitness[] fitness,
			StringBuilder output)
		{
			//base.OnEmittingGenome(p, genome, fitness, output);
			output.Append("Genome:").AppendLine(BLANK).AppendLine(genome.ToAlphaParameters());

			if (genome.Root is IReducibleEvaluation<IEvaluate<double>> r && r.TryGetReduced(Catalog, out var reduced))
			{
				output
					.Append("Reduced:")
					.AppendLine(BLANK)
					.AppendLine(AlphaParameters.ConvertTo(reduced.ToStringRepresentation()));
			}
		}
	}
}
