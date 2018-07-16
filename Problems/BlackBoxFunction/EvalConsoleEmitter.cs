using Open.Threading;
using Solve;
using Solve.Evaluation;
using Solve.Experiment.Console;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace BlackBoxFunction
{
	public class EvalConsoleEmitter : ConsoleEmitterBase<EvalGenome>
	{
		public EvalConsoleEmitter(uint sampleMinimum = 50)
			: base(sampleMinimum )
		{

		}
	}
}
