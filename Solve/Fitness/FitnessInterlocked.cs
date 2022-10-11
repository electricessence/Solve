using Open.Numeric;
using System;
using System.Collections.Immutable;
using System.Threading;

namespace Solve;

public class FitnessInterlocked : Fitness
{
	public FitnessInterlocked(ImmutableArray<Metric> metrics)
		: base(metrics) { }

	public FitnessInterlocked(ImmutableArray<Metric> metrics, ProcedureResults results)
		: base(metrics, results) { }

	public FitnessInterlocked(ImmutableArray<Metric> metrics, params double[] values)
		: base(metrics, new ProcedureResults(values.AsSpan(), 1)) { }

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

	public override ProcedureResults Merge(ReadOnlySpan<double> other, int count = 1)
	{
		ProcedureResults r;
		ProcedureResults sum;
		do
		{
			r = _results;
			sum = r.Count == 0
				? new ProcedureResults(other, count)
				: r.Add(other, count);
		}
		while (r != Interlocked.CompareExchange(ref _results, sum, r));
		return sum;
	}

	public new FitnessInterlocked Clone()
		=> new(Metrics, _results);
}
