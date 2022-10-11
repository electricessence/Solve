using System.Diagnostics;

namespace Solve.Experiment.Console;

[DebuggerDisplay("({Begin.Left}, {Begin.Top}) - ({End.Left}, {End.Top})")]
public record CursorRange
{
	public CursorRange(Cursor begin, Cursor end)
	{
		Begin = begin;
		End = end;
	}

	public Cursor Begin { get; }
	public Cursor End { get; }
}
