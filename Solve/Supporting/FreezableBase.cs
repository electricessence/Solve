using System;
using System.Threading;

namespace Solve;

public abstract class FreezableBase : IFreezable
{
	int _frozenState;
	public bool IsFrozen => _frozenState == 1;

	// It's concevable that mutliple threads could 're-attempt' to freeze a object 'in the wild'.
	public void Freeze()
	{
		if (_frozenState == 0 && Interlocked.CompareExchange(ref _frozenState, 1, 0) == 0)
		{
			OnBeforeFreeze(); // Ensure this is only called once.
		}
	}

	protected void AssertIsNotFrozen()
	{
		if (IsFrozen)
			throw new InvalidOperationException("Object is frozen.");
	}

	protected void AssertIsFrozen()
	{
		if (!IsFrozen)
			throw new InvalidOperationException("Object is not frozen.");
	}

	protected virtual void OnBeforeFreeze() { }
}
