using System;

namespace EvaluationFramework
{
	public abstract class OperationBase<TContext, TResult>
		: EvaluationBase<TContext, TResult>, IFunction<TContext, TResult>, ISymbolized, IReducibleEvaluation<TContext, TResult>
	{

		protected OperationBase(char symbol, string symbolString) : base()
		{
			if (symbolString == null)
				throw new ArgumentNullException("symbolString");

			Symbol = symbol;
			SymbolString = symbolString;
		}

		public char Symbol { get; private set; }
		public string SymbolString { get; private set; }

		protected virtual string ToStringInternal(object contents)
		{
			return string.Format("{0}({1})", SymbolString, contents);
		}
		
		public override string ToString(TContext context)
		{
			return ToStringInternal(Evaluate(context));
		}


		public IEvaluate<TContext, TResult> AsReduced()
		{
			var r = Reduction();
			if (r != null && r.ToStringRepresentation() == this.ToStringRepresentation()) r = this;
			return r ?? this;
		}

		// Override this if reduction is possible.  Return null if you can't reduce.
		public virtual IEvaluate<TContext, TResult> Reduction()
		{
			return null;
		}
	}

}