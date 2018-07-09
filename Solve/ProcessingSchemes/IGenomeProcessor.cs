using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	public interface IGenomeProcessor<TGenome>
		where TGenome : class, IGenome
	{
		void Post(TGenome genome);

		Task PostAsync(TGenome genome);
	}
}
