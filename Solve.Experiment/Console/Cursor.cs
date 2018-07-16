using System;
using System.Diagnostics;

namespace Solve.Experiment.Console
{
	[DebuggerDisplay("{Left}, {Top}")]
	public struct Cursor : IComparable<Cursor>, IEquatable<Cursor>
	{
		public static readonly object Sync = new object();

		Cursor(int left, int top)
		{
			Left = left;
			Top = top;
		}

		public readonly int Left;
		public readonly int Top;

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
		{
			if (Top < other.Top) return -1;
			if (Top > other.Top) return +1;
			if (Left < other.Left) return -1;
			if (Left > other.Left) return +1;
			return 0;
		}

		public bool Equals(Cursor other)
		{
			return CompareTo(other) == 0;
		}

		public static bool operator >(Cursor a, Cursor b)
		{
			return a.CompareTo(b) == 1;
		}

		public static bool operator <(Cursor a, Cursor b)
		{
			return a.CompareTo(b) == -1;
		}

		public static bool operator >=(Cursor a, Cursor b)
		{
			return a.CompareTo(b) >= 0;
		}

		public static bool operator <=(Cursor a, Cursor b)
		{
			return a.CompareTo(b) <= 0;
		}
	}
}
