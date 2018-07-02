using Open.Numeric;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Solve
{
	public class FitnessContainer
	{
		public FitnessContainer(IReadOnlyList<Metric> metrics)
		{
			Metrics = metrics;
		}

		public FitnessContainer(IReadOnlyList<Metric> metrics, ProcedureResults results)
			: this(metrics)
		{
			Results = results;
		}

		public FitnessContainer(IReadOnlyList<Metric> metrics, params double[] values)
			: this(metrics, new ProcedureResults(values, 1))
		{

		}

		public IReadOnlyList<Metric> Metrics { get; }

		protected ProcedureResults _results;
		public ProcedureResults Results
		{
			get => _results;
			set
			{
				if (value.Count != Metrics.Count)
					throw new ArgumentException("The results size does not match the metrics.", nameof(value));

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
			var sb = MetricAverages.Select(mv => String.Format(mv.Metric.Format, mv.Value)).ToStringBuilder(", ");
			var c = _results.Count;
			if (c > 0)
			{
				if (c == 1)
					sb.AppendFormat(" (1 sample)", c);
				else
					sb.AppendFormat(" ({0:n0} samples)", c);
			}
			return sb.ToString();
		}

		public FitnessContainer Clone() => new FitnessContainer(Metrics, _results);
	}
}
