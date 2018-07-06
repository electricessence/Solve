using System.Diagnostics;

namespace Solve.Experiment.Console
{
	[DebuggerDisplay("({Begin.Left}, {Begin.Top}) - ({End.Left}, {End.Top})")]
	public class CursorRange
	{
		public CursorRange(in Cursor begin, in Cursor end)
		{
			Begin = begin;
			End = end;
		}

		public Cursor Begin;
		public Cursor End;
	}
}
