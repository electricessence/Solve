namespace Solve
{
	public abstract class GenomeFitnessBroadcasterBase<TGenome> : BroadcasterBase<IGenomeFitness<TGenome, Fitness>>
		where TGenome : class, IGenome
	{

		protected GenomeFitnessBroadcasterBase()
		{
		}

	}
}
