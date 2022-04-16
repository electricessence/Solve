namespace Solve;

/*
     * It's important to have an interface that ensures an object is in flux or won't change.
     * This way we can avoid synchronization woes and use virtually immutable objects instead.
     */

public interface IFreezable
{
	// True if frozen.
	bool IsFrozen { get; }

	/*
         * Should prevent further modifications to the genome.
	 * Needs to be thread safe.
         */
	void Freeze();
}
