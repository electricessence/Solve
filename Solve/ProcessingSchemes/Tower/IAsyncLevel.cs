using System.Threading.Tasks;

namespace Solve.ProcessingSchemes
{
	public interface IAsyncLevel<TGenome> : ILevel<TGenome>
		where TGenome : class, IGenome
	{
		ValueTask PostAsync(LevelProgress<TGenome> contender);

		new IAsyncLevel<TGenome> NextLevel { get; }

		ILevel<TGenome> ILevel<TGenome>.NextLevel => NextLevel;
	}
}
