using System.Threading.Tasks.Dataflow;

namespace Solve
{
	public interface IProcessingPipeline<TGenome>
		: ITargetBlock<GenomeFitness<TGenome>>, ISourceBlock<GenomeFitness<TGenome>>
		where TGenome : IGenome
	{
		GenomeFitness<TGenome> Champion { get; }
	}
}
