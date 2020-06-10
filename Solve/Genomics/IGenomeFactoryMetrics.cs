using Solve.Metrics;
using System;
using System.Collections.Immutable;

namespace Solve
{
	public struct QueueCount
	{
		public QueueCount(string key, long value)
		{
			Key = key;
			Value = value;
		}

		public string Key { get; }
		public long Value { get; }
	}

	public interface IGenomeFactoryMetrics
	{
		DateTime Timestamp { get; }

		long BreedingStock { get; }
		long InternalQueueCount { get; }
		long AwaitingVariation { get; }
		long AwaitingMutation { get; }

		ImmutableArray<QueueCount> QueueStates { get; }

		SuccessFailCount GenerateNew { get; }
		SuccessFailCount Mutation { get; }
		SuccessFailCount Crossover { get; }

		long ExternalProducerQueried { get; }

	}
}
