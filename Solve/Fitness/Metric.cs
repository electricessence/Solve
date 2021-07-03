using System;
using System.Diagnostics.Contracts;

namespace Solve
{
	public struct Metric
	{
		public Metric(ushort id, string name, string format, double maxValue = double.PositiveInfinity, double tolerance = double.NaN)
		{
			if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Cannot be null, empty or whitespace.", nameof(name));
			if (string.IsNullOrWhiteSpace(format)) throw new ArgumentException("Cannot be null, empty or whitespace.", nameof(format));
			if (!format.Contains("{0")) throw new ArgumentException("Invalid format string.");
			if (tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Must be zero or greater.");
			Contract.EndContractBlock();
			// ReSharper disable once ReturnValueOfPureMethodIsNotUsed
			_ = string.Format(format, 1d); // validate format...

			ID = id;
			Name = name;
			Format = format;
			MaxValue = maxValue;
			Tolerance = tolerance;
			Convergence = !double.IsNaN(tolerance);
		}

		public ushort ID { get; }
		public string Name { get; }
		public string Format { get; }

		public double MaxValue { get; }
		public double Tolerance { get; }
		public bool Convergence { get; }
	}
}
