using System;
using System.Collections.Generic;

namespace EvaluationFramework.BooleanOperators
{
	public class AtLeast : CountingBase
	{
		public const string PREFIX = "AtLeast";
		public AtLeast(int count, IEnumerable<IEvaluate<bool>> children = null)
			: base(PREFIX, count, children)
		{
			if (count < 1)
				throw new ArgumentOutOfRangeException("count", count, "Count must be at least 1.");
		}

		protected override bool EvaluateInternal(object context)
		{
			int count = 0;
			foreach (var result in ChildResults(context))
			{
				if ((bool)result) count++;
				if (count == Count) return true;
			}

			return false;
		}

	}

}