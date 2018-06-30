using Open.Numeric;
using System;
using System.Threading;

namespace Solve
{
	public class FitnessContainerInterlocked : FitnessContainer
	{
		public override int IncrementRejection()
			=> Interlocked.Increment(ref _rejectionCount);

		public override ProcedureResults Merge(ProcedureResults other)
		{
			ProcedureResults r;
			ProcedureResults sum;
			do
			{
				r = _results;
				sum = r == null ? other : (r + other);
			}
			while (r != Interlocked.CompareExchange(ref _results, sum, r));
			return sum;
		}

		public override ProcedureResults Merge(ReadOnlySpan<double> other, int count = 1)
		{
			ProcedureResults r;
			ProcedureResults sum;
			do
			{
				r = _results;
				sum = r == null
					? new ProcedureResults(other, count)
					: r.Add(other, count);
			}
			while (r != Interlocked.CompareExchange(ref _results, sum, r));
			return sum;
		}
	}
}
