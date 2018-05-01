using System.Threading.Tasks.Dataflow;

namespace Solve
{
	public interface IProcessingScheme<TGenome>
		: IPropagatorBlock<GenomeFitness<TGenome>, GenomeFitness<TGenome>>
		where TGenome : IGenome
	{

	}
}
