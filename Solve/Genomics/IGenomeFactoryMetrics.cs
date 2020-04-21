using Solve.Metrics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Solve
{
	public interface IGenomeFactoryMetrics
	{
		DateTime Timestamp { get; }

		long BreedingStock { get; }
		long InternalQueueCount { get; }
		long AwaitingVariation { get; }
		long AwaitingMutation { get; }

		ImmutableArray<KeyValuePair<string, long>> QueueStates { get; }

		SuccessFailCount GenerateNew { get; }
		SuccessFailCount Mutation { get; }
		SuccessFailCount Crossover { get; }

		long ExternalProducerQueried { get; }

	}
}
