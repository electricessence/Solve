namespace EvaluationFramework.BooleanOperators
{
	public class Not : FunctionBase<bool>
	{
		public const char SYMBOL = '!';
		public const string SYMBOL_STRING = "!";

		public Not(IEvaluate<bool> contents)
			: base(Not.SYMBOL, Not.SYMBOL_STRING, contents)
		{

		}

		protected override bool EvaluateInternal(object context)
		{
			return !base.Evaluate(context);
		}

	}
	
}