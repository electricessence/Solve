using System;
using System.Collections.Generic;

namespace Solve
{
	public interface IGenomeFactoryPriorityQueue<TGenome>
		where TGenome : class, IGenome
	{
		void EnqueueChampion(in TGenome genome);
		void EnqueueChampion(in ReadOnlySpan<TGenome> genomes);

		void EnqueueVariations(in TGenome genome);
		void EnqueueVariations(in ReadOnlySpan<TGenome> genomes);

		void EnqueueForVariation(in TGenome genome);
		void EnqueueForVariation(in ReadOnlySpan<TGenome> genomes);

		bool Mutate(in TGenome genome, in int maxCount = 1);

		void EnqueueForMutation(in TGenome genome, in int count = 1);
		void EnqueueForMutation(in ReadOnlySpan<TGenome> genomes);

		void EnqueueForBreeding(in TGenome genome, in int count = 1);
		void EnqueueForBreeding(in ReadOnlySpan<TGenome> genomes);

		void Breed(in TGenome genome = null, in int maxCount = 1);
		void Breed(in ReadOnlySpan<TGenome> genomes);

		bool TryGetNext(out TGenome genome);

		List<Func<bool>> ExternalProducers { get; }
	}
}
