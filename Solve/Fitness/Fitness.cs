using Open.Memory;
using Open.Numeric;
using Open.Numeric.Precision;
using Open.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace Solve
{
	[DebuggerDisplay("{this.ToString()}")]
	public class Fitness : IComparable<Fitness>
	{
		public Fitness(in ImmutableArray<Metric> metrics, ProcedureResults results)
		{
			var len = results.Sum.Length;
			Debug.Assert(len == 0 || len == metrics.Length);
			Metrics = metrics;
			_results = results ?? throw new ArgumentNullException(nameof(results));
		}

		public Fitness(in ImmutableArray<Metric> metrics, params double[] values)
			: this(in metrics,
				  (values == null || values.Length == 0)
				  ? ProcedureResults.Empty
				  : new ProcedureResults(values, 1))
		{ }

		public Fitness(in ImmutableArray<Metric> metrics, in ImmutableArray<double> values)
			: this(in metrics,
				  (values == null || values.Length == 0)
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
				var r = value ?? throw new ArgumentNullException(nameof(value));
				Debug.Assert(value.Sum.Length == Metrics.Length);
				_results = r;
			}
		}

		public int SampleCount => _results?.Count ?? 0;

		public virtual ProcedureResults Merge(ProcedureResults other)
		{
			var r = _results;
			var sum = r.Count == 0 ? other : (r + other);
			_results = sum;
			return sum;
		}

		public virtual ProcedureResults Merge(in ReadOnlySpan<double> other, int count = 1)
		{
			var r = _results;
			var sum = r.Count == 0
				? new ProcedureResults(in other, count)
				: r.Add(in other, count);
			_results = sum;
			return sum;
		}

		public virtual ProcedureResults Merge(in ImmutableArray<double> other, int count = 1)
		{
			var r = _results;
			var sum = r.Count == 0
				? new ProcedureResults(in other, count)
				: r.Add(other, count);
			_results = sum;
			return sum;
		}

		public virtual ProcedureResults Merge(IReadOnlyList<double> other, int count = 1)
		{
			var r = _results;
			var sum = r.Count == 0
				? new ProcedureResults(other, count)
				: r.Add(other, count);
			_results = sum;
			return sum;
		}

		public IEnumerable<(Metric Metric, double Value)> MetricSums
		{
			get
			{
				var r = _results;
				return r.Count == 0
					? Metrics.ToArray().Select(m => (m, double.NaN))
					: Metrics.ToArray().Select((m, i) => (m, r.Sum[i]));
			}
		}

		public IEnumerable<(Metric Metric, double Value)> MetricAverages
		{
			get
			{
				var r = _results;
				return r.Count == 0
					? Metrics.ToArray().Select(m => (m, double.NaN))
					: Metrics.ToArray().Select((m, i) => (m, r.Average[i]));
			}
		}

		public override string ToString()
		{
			var c = _results?.Count ?? 0;
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

			if (this == other || Results == other.Results || SampleCount == 0 && other.SampleCount == 0)
				return 0;
			if (Results.Count == 0)
				return -1;
			if (other.Results.Count == 0)
				return +1;
			var v = CollectionComparer.Double.Compare(
				Results.Average,
				other.Results.Average);

			return v == 0 ? SampleCount.CompareTo(other.SampleCount) : v;
		}

		public bool HasConverged(uint minSamples = 100)
		{
			if (minSamples > SampleCount) return false;
			var c = false;
			foreach (var (Metric, Value) in MetricAverages.Where(m => m.Metric.Convergence))
			{
				c = true;
				var convergence = Metric.MaxValue;
				var tolerance = Metric.Tolerance;
				if (Value > convergence + double.Epsilon)
					throw new Exception("Score has exceeded convergence value: " + Value);
				// ReSharper disable once CompareOfFloatsByEqualityOperator
				if (Value == convergence || Value.IsNearEqual(convergence, 0.0000001)
					&& Value.ToString(CultureInfo.InvariantCulture) == convergence.ToString(CultureInfo.InvariantCulture))
					continue;
				if (double.IsNaN(tolerance)) // not necessary but shows more explicit intent.
					continue;
				if (Value < convergence - tolerance)
					return false;
			}
			return c;
		}

		public bool IsSuperiorTo(Fitness other)
			=> CompareTo(other) > 0;
	}
}
