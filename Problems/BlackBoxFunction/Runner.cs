using Solve.Evaluation;
using Solve.Experiment.Console;
using Solve.ProcessingSchemes;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace BlackBoxFunction
{
	[SuppressMessage("ReSharper", "UnusedMember.Local")]
	internal class Runner : RunnerBase<EvalGenome>
	{

		static double AB(IReadOnlyList<double> p)
		{
			var a = p[0];
			var b = p[1];
			return a * b;
		}

		static double SqrtA2B2(IReadOnlyList<double> p)
		{
			var a = p[0];
			var b = p[1];
			return Math.Sqrt(a * a + b * b);
		}

		static double SqrtA2B2C2(IReadOnlyList<double> p)
		{
			var a = p[0];
			var b = p[1];
			var c = p[2];
			return Math.Sqrt(a * a + b * b + c * c);
		}

		static double SqrtA2B2A2B1(IReadOnlyList<double> p)
		{
			var a = p[0];
			var b = p[1];
			return Math.Sqrt(a * a + b * b + a + 2) + b + 1;
		}

		readonly ushort _minSamples;

		protected Runner(ushort minSamples, ushort minConvSamples = 20) : base(minConvSamples)
		{
			_minSamples = minSamples;
		}

		public void Init()
		{

			var factory = new EvalGenomeFactory<EvalGenome>();
			var emitter = new EvalConsoleEmitter(factory, _minSamples);
			var scheme = new TowerProcessingScheme<EvalGenome>(factory, (100, 40, 2));
			scheme.AddProblem(Problem.Create(SqrtA2B2));

			Init(scheme, emitter, factory.Metrics);
		}

		static Task Main()
		{
			var runner = new Runner(1);
			runner.Init();
			var message = string.Format(
				"Solving Black-Box Problem... (minimum {0:n0} samples before displaying)",
				runner._minSamples);
			return runner.Start(message);
		}

	}
}
