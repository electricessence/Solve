using Open.Memory;
using Open.Numeric;
using Open.Text;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Solve;

[DebuggerDisplay("{this.ToString()}")]
public class Fitness : IComparable<Fitness>
{
	public Fitness(ImmutableArray<Metric> metrics, ProcedureResults results)
	{
		int len = results.Sum.Length;
		Debug.Assert(len == 0 || len == metrics.Length);
		Metrics = metrics;
		_results = results ?? throw new ArgumentNullException(nameof(results));
	}

	public Fitness(ImmutableArray<Metric> metrics, params double[] values)
		: this(metrics,
			  (values is null || values.Length == 0)
			  ? ProcedureResults.Empty
			  : new ProcedureResults(values.AsSpan(), 1))
	{ }

	public Fitness(ImmutableArray<Metric> metrics, ImmutableArray<double> values)
		: this(metrics,
			  values.IsDefaultOrEmpty
			  ? ProcedureResults.Empty
			  : new ProcedureResults(values, 1))
	{ }

	public ImmutableArray<Metric> Metrics { get; }

	protected ProcedureResults _results;
	public ProcedureResults Results
	{
		get => _results;
		set
		{
			ProcedureResults r = value ?? throw new ArgumentNullException(nameof(value));
			Debug.Assert(value.Sum.Length == Metrics.Length);
			_results = r;
		}
	}

	public int SampleCount => _results?.Count ?? 0;

	public virtual ProcedureResults Merge(ProcedureResults other)
	{
		ProcedureResults r = _results;
		// Equivalent to `r + other`, but routed through Add(ReadOnlySpan<double>, int) instead
		// of the `+` operator: the operator internally boxes both operands' Sum
		// (ImmutableArray<double> is a struct passed to an IReadOnlyList<double> parameter),
		// while AsSpan() lets this side of the sum pass through unboxed -- one fewer boxed
		// array per merge on these long-lived champion Fitness objects.
		ProcedureResults sum = r.Count == 0 ? other : r.Add(other.Sum.AsSpan(), other.Count);
		_results = sum;
		return sum;
	}

	public virtual ProcedureResults Merge(ReadOnlySpan<double> other, int count = 1)
	{
		ProcedureResults r = _results;
		ProcedureResults sum = r.Count == 0
			? new ProcedureResults(other, count)
			: r.Add(other, count);
		_results = sum;
		return sum;
	}

	public virtual ProcedureResults Merge(ImmutableArray<double> other, int count = 1)
	{
		ProcedureResults r = _results;
		// other.AsSpan() routes to Add(ReadOnlySpan<double>, int) instead of
		// Add(IReadOnlyList<double>, int) -- the latter would box `other` (a struct) to pass
		// it as an interface-typed parameter. This is the overload actually exercised on the
		// hot evaluation path (TowerScheme.Level.cs's ProcessContenderSafelyAsync), so avoiding
		// that per-merge boxed allocation matters on long-lived champion Fitness objects.
		ProcedureResults sum = r.Count == 0
			? new ProcedureResults(other, count)
			: r.Add(other.AsSpan(), count);
		_results = sum;
		return sum;
	}

	public virtual ProcedureResults Merge(IReadOnlyList<double> other, int count = 1)
	{
		ProcedureResults r = _results;
		ProcedureResults sum = r.Count == 0
			? new ProcedureResults(other, count)
			: r.Add(other, count);
		_results = sum;
		return sum;
	}

	public IEnumerable<(Metric Metric, double Value)> MetricSums
	{
		get
		{
			ProcedureResults r = _results;
			return r.Count == 0
				? Metrics.Select(m => (m, double.NaN))
				: Metrics.Select((m, i) => (m, r.Sum[i]));
		}
	}

	public IEnumerable<(Metric Metric, double Value)> MetricAverages
	{
		get
		{
			ProcedureResults r = _results;
			return r.Count == 0
				? Metrics.Select(m => (m, double.NaN))
				: Metrics.Select((m, i) => (m, r.Average[i]));
		}
	}

	public override string ToString()
	{
		int c = _results?.Count ?? 0;
		if (c == 0) return base.ToString()!;
		var sb = MetricAverages.Select(mv => string.Format(mv.Metric.Format, mv.Value)).ToStringBuilder(", ");
		if (c == 1)
			sb.Append(" (1 sample)");
		else
			sb.AppendFormat(" ({0:n0} samples)", c);
		return sb.ToString();
	}

	public Fitness Clone()
		=> new(Metrics, _results);

	public int CompareTo(Fitness? other)
	{
		if (other is null) return +1;

		if (this == other
		|| Results == other.Results
		|| SampleCount == 0 && other.SampleCount == 0)
		{
			return 0;
		}

		if (Results.Count == 0)
			return -1;
		if (other.Results.Count == 0)
			return +1;

		int v = CollectionComparer.Double.Compare(
			Results.Average,
			other.Results.Average);

		return v == 0 ? SampleCount.CompareTo(other.SampleCount) : v;
	}

	public bool HasConverged(uint minSamples = 100)
	{
		if (minSamples > SampleCount) return false;
		bool c = false;
		foreach ((Metric Metric, double Value) in MetricAverages.Where(m => m.Metric.Convergence))
		{
			c = true;
			double maxValue = Metric.MaxValue;
			double tolerance = Metric.Tolerance;

			if (Value > maxValue)
			{
				// A value above the metric's maximum cannot improve further — treat as
				// converged. (Transform floating-point overshoot must not crash a run.)
				continue;
			}

			if (Value < maxValue - tolerance)
				return false;
		}

		return c;
	}

	public bool IsSuperiorTo(Fitness other)
		=> CompareTo(other) > 0;

	public override bool Equals(object? obj)
		=> ReferenceEquals(this, obj);

	public override int GetHashCode()
		=> HashCode.Combine(Metrics, _results);

	public static bool operator ==(Fitness left, Fitness right)
		=> left is null ? right is null : left.Equals(right);

	public static bool operator !=(Fitness left, Fitness right)
		=> !(left == right);

	public static bool operator <(Fitness left, Fitness right)
		=> left is null ? right is not null : left.CompareTo(right) < 0;

	public static bool operator <=(Fitness left, Fitness right)
		=> left is null || left.CompareTo(right) <= 0;

	public static bool operator >(Fitness left, Fitness right)
		=> left?.CompareTo(right) > 0;

	public static bool operator >=(Fitness left, Fitness right)
		=> left is null ? right is null : left.CompareTo(right) >= 0;
}
