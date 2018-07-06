using System;
using System.Collections.Generic;

namespace Solve
{
	public interface IGenomeFactoryPriorityQueue<TGenome>
		where TGenome : class, IGenome
	{
		void EnqueueChampion(TGenome genome);
		void EnqueueChampion(in ReadOnlySpan<TGenome> genomes);

		void EnqueueVariations(TGenome genome);
		void EnqueueVariations(in ReadOnlySpan<TGenome> genomes);

		void EnqueueForVariation(TGenome genome);
		void EnqueueForVariation(in ReadOnlySpan<TGenome> genomes);

		bool Mutate(TGenome genome, int maxCount = 1);

		void EnqueueForMutation(TGenome genome, int count = 1);
		void EnqueueForMutation(in ReadOnlySpan<TGenome> genomes);

		void EnqueueForBreeding(TGenome genome, int count = 1);
		void EnqueueForBreeding(in ReadOnlySpan<TGenome> genomes);

		void Breed(TGenome genome = null, int maxCount = 1);
		void Breed(in ReadOnlySpan<TGenome> genomes);

		bool TryGetNext(out TGenome genome);

		List<Func<bool>> ExternalProducers { get; }
	}
}
