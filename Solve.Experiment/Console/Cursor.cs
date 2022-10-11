using System;
using System.Diagnostics;

namespace Solve.Experiment.Console;

[DebuggerDisplay("{Left}, {Top}")]
public readonly record struct Cursor : IComparable<Cursor>
{
	public static readonly object Sync = new();

	Cursor(int left, int top)
	{
		Left = left;
		Top = top;
	}

	public int Left { get; }
	public int Top { get; }

	public static Cursor Current
	{
		get
		{
			lock (Sync) return new Cursor(System.Console.CursorLeft, System.Console.CursorTop);
		}
	}

	public void MoveTo()
	{
		lock (Sync) System.Console.SetCursorPosition(Left, Top);
	}

	public int CompareTo(Cursor other)
		=> Top < other.Top ? -1
		 : Top > other.Top ? +1
		 : Left < other.Left ? -1
		 : Left > other.Left ? +1
		 : 0;

	public static bool operator >(Cursor a, Cursor b) => a.CompareTo(b) == 1;

	public static bool operator <(Cursor a, Cursor b) => a.CompareTo(b) == -1;

	public static bool operator >=(Cursor a, Cursor b) => a.CompareTo(b) >= 0;

	public static bool operator <=(Cursor a, Cursor b) => a.CompareTo(b) <= 0;
}
