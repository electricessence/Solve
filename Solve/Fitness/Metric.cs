using System;

namespace Solve
{
	public struct Metric
	{
		public Metric(ushort id, string name, string format, double maxValue = double.PositiveInfinity)
		{
			if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Cannot be null, empty or whitespace.", nameof(name));
			if (string.IsNullOrWhiteSpace(format)) throw new ArgumentException("Cannot be null, empty or whitespace.", nameof(format));
			if (!format.Contains("{0")) throw new ArgumentException("Invalid format string.");
			// ReSharper disable once ReturnValueOfPureMethodIsNotUsed
			string.Format(format, 1d); // validate format...

			ID = id;
			Name = name;
			Format = format;
			MaxValue = maxValue;
		}

		public ushort ID { get; }
		public string Name { get; }
		public string Format { get; }

		public double MaxValue { get; }
	}
}
