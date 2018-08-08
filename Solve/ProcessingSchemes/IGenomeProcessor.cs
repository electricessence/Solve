using System.Diagnostics.CodeAnalysis;

namespace Solve.ProcessingSchemes
{
	[SuppressMessage("ReSharper", "UnusedMemberInSuper.Global")]
	public interface IGenomeProcessor<in TGenome>
		where TGenome : class, IGenome
	{
		void Post(TGenome genome);

		//Task PostAsync(TGenome genome, CancellationToken token);
	}
}
