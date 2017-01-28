using System.Threading.Tasks;

namespace Solve
{
	public delegate Task<IFitness> GenomeTestDelegate<TGenome>(TGenome candidate, long sampleId = 0, bool mergeWithGlobal = false) where TGenome : IGenome;
}