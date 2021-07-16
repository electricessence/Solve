using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
    public interface IFactoryDriver<TGenome>
		where TGenome : class, IGenome
	{
		public Task Start(CancellationToken token);

	}
}
