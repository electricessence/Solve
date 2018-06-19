using System;
using System.Collections.Generic;

namespace Solve
{
	public interface IGenomeFactoryPriorityQueue<TGenome>
		where TGenome : class, IGenome
	{
		void EnqueueChampion(TGenome genome);
		void EnqueueChampion(ReadOnlySpan<TGenome> genomes);

		void EnqueueVariations(TGenome genome);
		void EnqueueVariations(ReadOnlySpan<TGenome> genomes);

		void EnqueueForVariation(TGenome genome);
		void EnqueueForVariation(ReadOnlySpan<TGenome> genomes);

		void EnqueueForMutation(TGenome genome);
		void EnqueueForMutation(ReadOnlySpan<TGenome> genomes);

		void EnqueueForBreeding(TGenome genome, int count = 1);
		void EnqueueForBreeding(ReadOnlySpan<TGenome> genomes);

		void Breed(TGenome genome = null);
		void Breed(ReadOnlySpan<TGenome> genomes);

		bool TryGetNext(out TGenome genome);

		List<Func<bool>> ExternalProducers { get; }
	}
}
