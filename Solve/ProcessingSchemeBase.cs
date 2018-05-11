namespace Solve
{
	public abstract class ProcessingSchemeBase<TGenome> : BroadcasterBase<IGenomeFitness<TGenome, Fitness>>
		where TGenome : class, IGenome
	{

		protected ProcessingSchemeBase()
		{
		}

	}
}
