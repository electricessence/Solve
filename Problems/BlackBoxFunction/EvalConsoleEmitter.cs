using Open.Evaluation.Core;
using Solve.Evaluation;
using Solve.Experiment.Console;
using System.Text;
using System.Text.RegularExpressions;

namespace BlackBoxFunction;

public partial class EvalConsoleEmitter(ICatalog<IEvaluate<double>> catalog, uint sampleMinimum = 50)
	: ConsoleEmitterBase<EvalGenome<double>>(sampleMinimum)
{
	readonly ICatalog<IEvaluate<double>> Catalog = catalog;

	public EvalConsoleEmitter(NumericEvalGenomeFactory factory, uint sampleMinimum = 50)
		: this(factory.Catalog, sampleMinimum)
	{
	}

	static readonly Regex SimpleProductsPattern = SimpleProductsRegex();
	static readonly Regex StripParensPattern = StripParensRegex();
	//static readonly Regex SuperScriptDigitPattern = new Regex(@"\^[0-9\.]+", RegexOptions.Compiled);
	//static readonly Regex CombineMultiplePattern = new Regex(@"(\d+\s\*\s)[a-z]+", RegexOptions.Compiled);
	//static readonly Regex DivisionPattern = new Regex(@"\((\w+)\^-(\d+)\)", RegexOptions.Compiled);
	//static readonly Regex DivisionTailedPattern = new Regex($@" \* \(1/(\w+[{Exponent.SuperScriptDigits}]*)\)", RegexOptions.Compiled);
	//static readonly Regex NegativeMultiplePattern = new Regex(@" \+ \(-(\w+) ([*/]) ", RegexOptions.Compiled);

	static string FormatGenomeString(string h)
	{
		//h = h
		//	.Replace(" + -", " - ")
		//	.Replace(" + (-1 * ", " - (");
		h = SimpleProductsPattern.Replace(h,
			m => m.Value.Replace(" * ", string.Empty));
		h = StripParensPattern.Replace(h,
			m => m.Groups[1].Value + m.Groups[2].Value);
		h = StripParensPattern.Replace(h,
			m => m.Groups[1].Value + m.Groups[2].Value);
		//h = CombineMultiplePattern.Replace(h,
		//	m => m.Value.Replace(" * ", string.Empty));
		//h = DivisionTailedPattern.Replace(h, m => $" / {m.Groups[1].Value}");
		//h = NegativeMultiplePattern.Replace(h, m => $" - ({m.Groups[1].Value} {m.Groups[2].Value} ");
		return h;
	}

	protected override void OnEmittingGenome(
		EvalGenome<double> genome,
		StringBuilder output)
	{
		//base.OnEmittingGenome(p, genome, fitness, output);
		output
			.Append("Genome:")
			.AppendLine(BLANK)
			.AppendLine(Format(genome.Root));

		if (genome.Root is IReducibleEvaluation<IEvaluate<double>> r && r.TryGetReduced(Catalog, out var reduced))
		{
			output
				.Append("Reduced:")
				.AppendLine(BLANK)
				.AppendLine(Format(reduced));
		}

		static string Format(IEvaluate<double> root)
		{
			var hash = root.ToStringRepresentation();
			var alpha = AlphaParameters.ConvertTo(hash);
			return FormatGenomeString(alpha);
		}
	}

	[GeneratedRegex(@"\((\w+[⁰¹²³⁴⁵⁶⁷⁸⁹]*)\)(\)|\s)", RegexOptions.Compiled)]
	private static partial Regex StripParensRegex();

	[GeneratedRegex(@"(\d+|[a-z]+)(\s\*\s[a-z]+)+", RegexOptions.Compiled)]
	private static partial Regex SimpleProductsRegex();
}
