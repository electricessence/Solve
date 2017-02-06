namespace EvaluationFramework.BooleanOperators
{
	public class Not<TContext> : FunctionBase<TContext, bool>
	{
		public Not(IEvaluate<TContext, bool> contents)
			: base(Not.SYMBOL, Not.SYMBOL_STRING, contents)
		{

		}

		public override bool Evaluate(TContext context)
		{
			return !base.Evaluate(context);
		}
	}

	public class Not : Not<bool>
	{
		public const char SYMBOL = '!';
		public const string SYMBOL_STRING = "!";

		public Not(IEvaluate<bool, bool> contents) : base(contents)
		{
		}

		public static Not<TContext> Using<TContext>(IEvaluate<TContext, bool> evaluation)
		{
			return new Not<TContext>(evaluation);
		}
	}

	public static class NotExtensions
	{
		public static Not<TContext> Not<TContext>(this IEvaluate<TContext, bool> evaluation)
		{
			return new Not<TContext>(evaluation);
		}
	}

}