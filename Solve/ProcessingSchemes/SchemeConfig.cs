using System;
using System.Diagnostics.Contracts;

namespace Solve.ProcessingSchemes
{
	public interface ISchemeConfig
	{
		SchemeConfig.PoolSizing PoolSize { get; }
		ushort MaxLevels { get; }
		ushort MaxLevelLoss { get; }
		ushort MaxConsecutiveRejections { get; }
		ushort PercentRejectedBeforeElimination { get; }

		SchemeConfig.Values Immutable { get; }
	}

	public class SchemeConfig : ISchemeConfig
	{
		public struct Values : ISchemeConfig
		{
			public Values(PoolSizing poolSize, ushort maxLevels, ushort maxLevelLoss, ushort maxConsecutiveRejections, ushort percentRejectedBeforeElimination)
			{
				PoolSize = poolSize;
				MaxLevels = maxLevels;
				MaxLevelLoss = maxLevelLoss;
				MaxConsecutiveRejections = maxConsecutiveRejections;
				PercentRejectedBeforeElimination = percentRejectedBeforeElimination;
			}

			public PoolSizing PoolSize { get; }
			public ushort MaxLevels { get; }
			public ushort MaxLevelLoss { get; }
			public ushort MaxConsecutiveRejections { get; }
			public ushort PercentRejectedBeforeElimination { get; }

			public Values Immutable => this;
		}

		public struct PoolSizing
		{
			private const string MUST_BE_MULTIPLE_OF_2 = "Must be a mutliple of 2.";

			public PoolSizing(ushort first, ushort minimum, ushort step)
			{
				if (minimum < 2)
					throw new ArgumentOutOfRangeException(nameof(minimum), "Must be at least 2.");
				if (first % 2 == 1)
					throw new ArgumentException(MUST_BE_MULTIPLE_OF_2, nameof(first));
				if (minimum % 2 == 1)
					throw new ArgumentException(MUST_BE_MULTIPLE_OF_2, nameof(minimum));
				if (step % 2 == 1)
					throw new ArgumentException(MUST_BE_MULTIPLE_OF_2, nameof(step));
				if (first < minimum)
					throw new ArgumentException("Minumum must be less than or equal to First.", nameof(minimum));
				Contract.EndContractBlock();

				First = first;
				Minimum = minimum;
				Step = step;
			}

			public PoolSizing((ushort First, ushort Minimum, ushort Step) values)
				: this(values.First, values.Minimum, values.Step) { }

			public void Deconstruct(out ushort first, out ushort minimum, out ushort step)
			{
				first = First;
				minimum = Minimum;
				step = Step;
			}

			public ushort First { get; }
			public ushort Minimum { get; }
			public ushort Step { get; }

			public static implicit operator PoolSizing((ushort First, ushort Minimum, ushort Step) values)
				=> new(values);

			public static implicit operator (ushort First, ushort Minimum, ushort Step)(PoolSizing sizing)
				=> (sizing.First, sizing.Minimum, sizing.Step);

			public ushort GetPoolSize(int level)
			{
				var (First, Minimum, Step) = this;
				var maxDelta = First - Minimum;
				var decrement = level * Step;
				return decrement > maxDelta ? Minimum : (ushort)(First - decrement);
			}
		}


		public PoolSizing PoolSize { get; set; }
		public ushort MaxLevels { get; set; } = ushort.MaxValue;
		public ushort MaxLevelLoss { get; set; } = 3;
		public ushort MaxConsecutiveRejections { get; set; } = 10;

		public ushort PercentRejectedBeforeElimination { get; set; } = 70;

		public static implicit operator Values(SchemeConfig config) => config.Immutable;

		public SchemeConfig Clone() => new()
		{
			PoolSize = PoolSize,
			MaxLevels = MaxLevels,
			MaxLevelLoss = MaxLevelLoss,
			MaxConsecutiveRejections = MaxConsecutiveRejections,
			PercentRejectedBeforeElimination = PercentRejectedBeforeElimination
		};

		public Values Immutable => new(PoolSize, MaxLevels, MaxLevelLoss, MaxConsecutiveRejections, PercentRejectedBeforeElimination);

	}
}
