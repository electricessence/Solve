namespace EvaluationFramework.BooleanOperators
{
	public class Conditional<TContext, TResult> : FunctionBase<TContext, bool>, IFunction<TContext, TResult>
	{
		public const string SYMBOL = " ? ";
		public Conditional(
			IEvaluate<TContext, bool> evaluation,
			IEvaluate<TContext, TResult> ifTrue,
			IEvaluate<TContext, TResult> ifFalse)
			: base(SYMBOL, evaluation)
		{
			IfTrue = ifTrue;
			IfFalse = ifFalse;
		}

		public IEvaluate<TContext, TResult> IfTrue
		{
			get;
			private set;
		}
		public IEvaluate<TContext, TResult> IfFalse
		{
			get;
			private set;
		}

		public new TResult Evaluate(TContext context)
		{
			return base.Evaluate(context)
				? IfTrue.Evaluate(context)
				: IfFalse.Evaluate(context);
		}

		protected override string ToStringInternal(object evaluation)
		{
			return string.Format(
				"{0} ? {1} : {2}",
				evaluation,
				IfTrue.ToStringRepresentation(),
				IfFalse.ToStringRepresentation());
		}

		public override string ToString(TContext context)
		{
			return string.Format(
				"{0} ? {1} : {2}",
				base.Evaluate(context),
				IfTrue.Evaluate(context),
				IfFalse.Evaluate(context));
		}

	}

}