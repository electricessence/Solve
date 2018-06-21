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
				factoryQueue.EnqueueForMutation(top);
				factoryQueue.EnqueueForBreeding(top);

				var next = TriangularSelection.Descending.RandomOne(champions);
				factoryQueue.EnqueueForMutation(next);
				factoryQueue.EnqueueForBreeding(next);

				factoryQueue.EnqueueForMutation(champions);
				factoryQueue.EnqueueForBreeding(champions);

				return true;
			}

			return false;
		}
		#endregion

	}
}
