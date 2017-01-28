namespace Solve
{
	public interface IReducible<T>
	{
		T AsReduced(bool ensureClone = false);

		bool IsReducible { get; }

        void ReplaceReduced(T reduced);
	}

	public interface IReducibleGene<T> : IReducible<IGene>, IGene
	where T : IGene
	{
		T Reduce();

		new IReducibleGene<T> Clone();

	}

	public interface IReducibleGene : IReducibleGene<IGene>
	{
		
	}

}