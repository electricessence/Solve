using Open.Numeric;
using System;
using System.Collections.Immutable;
using System.Threading;

namespace Solve
{
	public class FitnessInterlocked : Fitness
	{
		public FitnessInterlocked(in ImmutableArray<Metric> metrics)
			: base(metrics)
		{
		}

		public FitnessInterlocked(in ImmutableArray<Metric> metrics, ProcedureResults results)
			: base(metrics, results)
		{
		}

		public FitnessInterlocked(in ImmutableArray<Metric> metrics, params double[] values)
			: base(metrics, new ProcedureResults(values, 1))
		{

		}

		public override int IncrementRejection()
			=> Interlocked.Increment(ref _rejectionCount);

		public override ProcedureResults Merge(ProcedureResults other)
		{
			ProcedureResults r;
			ProcedureResults sum;
			do
			{
				r = _results;
				sum = r.Count == 0 ? other : (r + other);
			}
			while (r != Interlocked.CompareExchange(ref _results, sum, r));
			return sum;
		}

		public override ProcedureResults Merge(in ReadOnlySpan<double> other, int count = 1)
		{
			ProcedureResults r;
			ProcedureResults sum;
			do
			{
				r = _results;
				sum = r.Count == 0
					? new ProcedureResults(in other, count)
					: r.Add(in other, count);
			}
			while (r != Interlocked.CompareExchange(ref _results, sum, r));
			return sum;
		}

		public new FitnessInterlocked Clone()
			=> new FitnessInterlocked(Metrics, _results);

	}
}
