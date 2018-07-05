using Open.Memory;
using Open.Numeric;
using Open.Numeric.Precision;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Solve
{
	[DebuggerDisplay("{this.ToString()}")]
	public class Fitness : IComparable<Fitness>
	{

		public Fitness(in IReadOnlyList<Metric> metrics, in ProcedureResults results)
		{
			Metrics = metrics;
			if (results != null) Results = results;
		}

		public Fitness(in IReadOnlyList<Metric> metrics, params double[] values)
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

		public virtual ProcedureResults Merge(in ProcedureResults other)
		{
			var r = _results;
			var sum = r == null ? other : (r + other);
			_results = sum;
			return sum;
		}

		public virtual ProcedureResults Merge(in ReadOnlySpan<double> other, in int count = 1)
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
			var sb = MetricAverages.Select(mv => String.Format(mv.Metric.Format, mv.Value)).ToStringBuilder(", ");
			if (c == 1)
				sb.AppendFormat(" (1 sample)", c);
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
			return v == 0 ? this.SampleCount.CompareTo(other.SampleCount) : v;
		}

		public bool HasConverged(in uint minSamples = 100, in double convergence = 1, in double tolerance = 0)
		{
			var results = Results;
			if (results == null || minSamples > SampleCount) return false;
			var ave = results.Average.Span;
			var len = ave.Length;
			for (var i = 0; i < len; i++)
			{
				var s = ave[i];
				if (s > convergence + double.Epsilon)
					throw new Exception("Score has exceeded convergence value: " + s);
				if (s.IsNearEqual(convergence, 0.0000001) && s.ToString() == convergence.ToString())
					continue;
				if (s < convergence - tolerance)
					return false;
			}
			return true;
		}

		public bool IsSuperiorTo(in Fitness other)
			=> CompareTo(other) > 0;
	}
}
