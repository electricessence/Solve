using Open.Memory;
using Open.Numeric;
using Open.Numeric.Precision;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

namespace Solve
{
	[DebuggerDisplay("{this.ToString()}")]
	[SuppressMessage("ReSharper", "MemberCanBeProtected.Global")]
	public class Fitness : IComparable<Fitness>
	{
		public Fitness(IReadOnlyList<Metric> metrics, ProcedureResults results)
		{
			Metrics = metrics;
			if (results != null) Results = results;
		}

		public Fitness(IReadOnlyList<Metric> metrics, params double[] values)
			: this(metrics, values == null || values.Length == 0 ? null : new ProcedureResults(values, 1)) { }

		public IReadOnlyList<Metric> Metrics { get; }

		protected ProcedureResults _results;
		public ProcedureResults Results
		{
			get => _results;
			set
			{
				Debug.Assert(value.Sum.Length == Metrics.Count);
				_results = value;
			}
		}

		public int SampleCount => Results?.Count ?? 0;

		protected int _rejectionCount;
		public int RejectionCount => _rejectionCount;
		public virtual int IncrementRejection() => ++_rejectionCount;

		public virtual ProcedureResults Merge(ProcedureResults other)
		{
			var r = _results;
			var sum = r == null ? other : (r + other);
			_results = sum;
			return sum;
		}

		public virtual ProcedureResults Merge(in ReadOnlySpan<double> other, int count = 1)
		{
			var r = _results;
			var sum = r == null
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
				return r == null || r.Count == 0
					? Metrics.Select(m => (m, double.NaN))
					: Metrics.Select((m, i) => (m, r.Sum.Span[i]));
			}
		}

		public IEnumerable<(Metric Metric, double Value)> MetricAverages
		{
			get
			{
				var r = _results;
				return r == null || r.Count == 0
					? Metrics.Select(m => (m, double.NaN))
					: Metrics.Select((m, i) => (m, r.Average.Span[i]));
			}
		}


		public override string ToString()
		{
			var c = _results?.Count ?? 0;
			if (c == 0) return base.ToString();
			var sb = MetricAverages.Select(mv => string.Format(mv.Metric.Format, mv.Value)).ToStringBuilder(", ");
			if (c == 1)
				sb.Append(" (1 sample)");
			else
				sb.AppendFormat(" ({0:n0} samples)", c);
			return sb.ToString();
		}

		public Fitness Clone() => new Fitness(Metrics, _results);

		public int CompareTo(Fitness other)
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			if (this == other || Results == other.Results)
				return 0;
			if (Results == null)
				return -1;
			if (other.Results == null)
				return +1;
			var v = MemoryComparer.Double.Compare(Results.Average, other.Results.Average);
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
				if (Value < convergence - tolerance)
					return false;
			}
			return c;
		}

		public bool IsSuperiorTo(Fitness other)
			=> CompareTo(other) > 0;
	}
}
