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
			if (champions.Length > 0)
			{
				factoryQueue.EnqueueForMutation(champions);
				if (champions.Length > 1)
				{
					factoryQueue.EnqueueForMutation(champions[1]);
					factoryQueue.Breed(champions);
				}

				return true;
			}

			return false;
		}
		#endregion

	}
}
