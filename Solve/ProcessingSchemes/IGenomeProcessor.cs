using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	[SuppressMessage("ReSharper", "UnusedMemberInSuper.Global")]
	public interface IGenomeProcessor<in TGenome>
		where TGenome : class, IGenome
	{
		void Post(TGenome genome);

		Task PostAsync(TGenome genome);
	}
}
