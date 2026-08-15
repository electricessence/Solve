using Open.Evaluation.Core;
using Solve.Evaluation;
using Solve.Experiment.Console;
using System.Text;
using System.Text.RegularExpressions;

namespace Multiplexer;

public class EvalConsoleEmitter(ICatalog<IEvaluate<bool>> catalog, uint sampleMinimum = 50) : ConsoleEmitterBase<EvalGenome<bool>>(sampleMinimum)
{
	public EvalConsoleEmitter(BooleanEvalGenomeFactory factory, uint sampleMinimum = 50)
		: this(factory.Catalog, sampleMinimum)
	{
	}

	static readonly Regex SimpleProductsPattern = new(@"(\d+|[a-z]+)(\s\*\s[a-z]+)+", RegexOptions.Compiled);
	static readonly Regex StripParensPattern = new(@"\((\w+[⁰¹²³⁴⁵⁶⁷⁸⁹]*)\)(\)|\s)", RegexOptions.Compiled);

	static string FormatGenomeString(string h)
	{
		h = SimpleProductsPattern.Replace(h,
			m => m.Value.Replace(" * ", string.Empty));
		h = StripParensPattern.Replace(h,
			m => m.Groups[1].Value + m.Groups[2].Value);
		h = StripParensPattern.Replace(h,
			m => m.Groups[1].Value + m.Groups[2].Value);
		return h;
	}

	protected override void OnEmittingGenome(
		EvalGenome<bool> genome,
		StringBuilder output)
	{
		output.Append("Genome:").AppendLine(BLANK).AppendLine(FormatGenomeString(genome.ToAlphaParameters()));

		if (genome.Root is IReducibleEvaluation<IEvaluate<bool>> r && r.TryGetReduced(catalog, out var reduced))
		{
			output
				.Append("Reduced:")
				.AppendLine(BLANK)
				.AppendLine(FormatGenomeString(AlphaParameters.ConvertTo(reduced.ToStringRepresentation())));
		}
	}
}
