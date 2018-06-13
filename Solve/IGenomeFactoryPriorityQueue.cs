namespace Solve
{
	public interface IGenomeFactoryPriorityQueue<TGenome>
		where TGenome : class, IGenome
	{
		void EnqueueChampion(params TGenome[] genomes);

		void EnqueueVariations(params TGenome[] genomes);
		void EnqueueForVariation(params TGenome[] genomes);
		void EnqueueForMutation(params TGenome[] genomes);
		void EnqueueForBreeding(params TGenome[] genomes);
		void EnqueueForBreeding(TGenome genomes, int count);

		void Breed(params TGenome[] genomes);

		bool TryGetNext(out TGenome genome);
	}
}
