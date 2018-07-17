using Solve.Evaluation;
using Solve.Experiment.Console;

namespace BlackBoxFunction
{
	public class EvalConsoleEmitter : ConsoleEmitterBase<EvalGenome>
	{
		public EvalConsoleEmitter(uint sampleMinimum = 50)
			: base(sampleMinimum)
		{

		}
	}
}
