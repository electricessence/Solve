using System.Threading.Tasks.Dataflow;

namespace Solve
{
	public interface IProcessingScheme<TGenome>
		: IPropagatorBlock<IGenomeFitness<TGenome, Fitness>, IGenomeFitness<TGenome, Fitness>>
		where TGenome : IGenome
	{

	}
}
