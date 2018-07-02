using Open.Numeric;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Solve
{
	[DebuggerDisplay("{this.ToString()}")]
	public class FitnessContainer
	{

		public FitnessContainer(IReadOnlyList<Metric> metrics, ProcedureResults results)
		{
			Metrics = metrics;
			if (results != null) Results = results;
		}

		public FitnessContainer(IReadOnlyList<Metric> metrics, params double[] values)
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

		public virtual ProcedureResults Merge(ReadOnlySpan<double> other, int count = 1)
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

		public FitnessContainer Clone() => new FitnessContainer(Metrics, _results);
	}
}
