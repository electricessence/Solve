using System.Diagnostics;

namespace Solve.Experiment.Console;

[DebuggerDisplay("({Begin.Left}, {Begin.Top}) - ({End.Left}, {End.Top})")]
public class CursorRange
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1242:Do not pass non-read-only struct by read-only reference.", Justification = "<Pending>")]
	public CursorRange(in Cursor begin, in Cursor end)
	{
		Begin = begin;
		End = end;
	}

	public Cursor Begin;
	public Cursor End;
}
