using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Solve
{
	[SuppressMessage("ReSharper", "UnusedMemberInSuper.Global")]
	[SuppressMessage("ReSharper", "UnusedParameter.Global")]
	public interface IGenomeFactoryPriorityQueue<TGenome>
		where TGenome : class, IGenome
	{
		void EnqueueChampion(TGenome genome);
		void EnqueueChampion(IEnumerable<TGenome> genomes);

		void EnqueueVariations(TGenome genome);
		void EnqueueVariations(IEnumerable<TGenome> genomes);

		void EnqueueForVariation(TGenome genome);
		void EnqueueForVariation(IEnumerable<TGenome> genomes);

		bool Mutate(TGenome genome, int maxCount = 1);

		void EnqueueForMutation(TGenome genome, int count = 1);
		void EnqueueForMutation(IEnumerable<TGenome> genomes);

		void EnqueueForBreeding(TGenome genome, int count = 1);
		void EnqueueForBreeding(IEnumerable<TGenome> genomes);

		void Breed(TGenome? genome = null, int maxCount = 1);
		void Breed(IEnumerable<TGenome> genomes);

		bool TryGetNext([NotNullWhen(true)] out TGenome? genome);

		List<Func<bool>> ExternalProducers { get; }
	}
}
