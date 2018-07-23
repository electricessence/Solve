using Open.Evaluation.Core;
using Solve;
using Solve.Evaluation;
using Solve.Experiment.Console;
using System;
using System.Text;
using System.Text.RegularExpressions;

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

		private const string SuperScriptDigits = "⁰¹²³⁴⁵⁶⁷⁸⁹";
		static readonly Regex SimpleProductsPattern = new Regex(@"(\w+\s\*\s)+[a-z]+", RegexOptions.Compiled);
		static readonly Regex StripParensPattern = new Regex($@"\((\w+[{SuperScriptDigits}]*)\)", RegexOptions.Compiled);
		static readonly Regex SuperScriptDigitPattern = new Regex(@"\^\d+", RegexOptions.Compiled);


		static string FormatGenomeString(string h)
		{
			h = h
				.Replace(" + (-1 * ", " - (")
				.Replace(" + (-", " - (");
			h = SimpleProductsPattern.Replace(h, m => m.Value.Replace(" * ", string.Empty));
			h = StripParensPattern.Replace(h, m => m.Groups[1].Value);
			h = SuperScriptDigitPattern.Replace(h, m =>
			{
				var s = m.Value.AsSpan();
				var len = s.Length;
				var r = new char[len - 1];
				for (var i = 1; i < len; i++)
				{
					var n = char.GetNumericValue(s[i]);
					r[i - 1] = SuperScriptDigits[(int)n];
				}


				return new string(r);
			});
			h = StripParensPattern.Replace(h, m => m.Groups[1].Value);
			return h;
		}

		protected override void OnEmittingGenome(IProblem<EvalGenome> p, EvalGenome genome, Fitness[] fitness,
			StringBuilder output)
		{
			//base.OnEmittingGenome(p, genome, fitness, output);
			output.Append("Genome:").AppendLine(BLANK).AppendLine(FormatGenomeString(genome.ToAlphaParameters()));

			if (genome.Root is IReducibleEvaluation<IEvaluate<double>> r && r.TryGetReduced(Catalog, out var reduced))
			{
				output
					.Append("Reduced:")
					.AppendLine(BLANK)
					.AppendLine(FormatGenomeString(AlphaParameters.ConvertTo(reduced.ToStringRepresentation())));
			}
		}
	}
}
