using System.Collections.Generic;

namespace EvaluationFramework.BooleanOperators
{
	public class AtMost : CountingBase
	{
		public const string PREFIX = "AtMost";
		public AtMost(int count, IEnumerable<IEvaluate<bool>> children = null)
			: base(PREFIX, count, children)
		{
		}

		protected override bool EvaluateInternal(object context)
		{
			int count = 0;
			foreach (var result in ChildResults(context))
			{
				if ((bool)result) count++;
				if (count > Count) return false;
			}

			return true;
		}

	}


}