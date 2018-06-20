namespace Solve.ProcessingSchemes
{
	public abstract class ProblemSpecificBroadcasterBase<TGenome>
		: BroadcasterBase<IGenomeFitness<TGenome>>
		where TGenome : class, IGenome
	{
		protected ProblemSpecificBroadcasterBase(
			ushort championPoolSize)
		{
			if (championPoolSize != 0)
				ChampionPool = new RankedPool<TGenome>(championPoolSize);
		}

		#region Champion Pool
		public readonly RankedPool<TGenome> ChampionPool;

		public bool ProduceFromChampions(IGenomeFactoryPriorityQueue<TGenome> factoryQueue)
		{
			if (ChampionPool == null) return false;

			var champions = ChampionPool.Ranked;
			var len = champions.Length;
			if (len > 0)
			{
				var top = champions[0];
				factoryQueue.Mutate(top, 3);
				factoryQueue.Breed(top, 3);

				if (len > 1)
				{
					var second = champions[1];
					factoryQueue.Mutate(second, 2);
					factoryQueue.Breed(second, 2);
				}

				var next = TriangularSelection.Descending.RandomOne(champions);
				factoryQueue.Mutate(next);
				factoryQueue.Breed(next);

				return true;
			}

			return false;
		}
		#endregion

	}
}
