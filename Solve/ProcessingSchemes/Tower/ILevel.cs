namespace Solve.ProcessingSchemes;

public interface ILevel<TGenome>
	where TGenome : class, IGenome
{
	public ILevel<TGenome> NextLevel { get; }
}
