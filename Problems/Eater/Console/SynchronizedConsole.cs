using System;
using System.Diagnostics;

namespace Eater
{
	public static class SynchronizedConsole
	{
		public static readonly object Sync = new object();

		[DebuggerDisplay("{Left}, {Top}")]
		public struct Cursor : IComparable<Cursor>, IEquatable<Cursor>
		{
			Cursor(int left, int top)
			{
				Left = left;
				Top = top;
			}

			public int Left;
			public int Top;

			public static Cursor Current
			{
				get
				{
					lock (Sync) return new Cursor(Console.CursorLeft, Console.CursorTop);
				}
			}

			public void MoveTo()
			{
				lock (Sync) Console.SetCursorPosition(Left, Top);
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

		[DebuggerDisplay("({Begin.Left}, {Begin.Top}) - ({End.Left}, {End.Top})")]
		public class Message
		{
			public Message(Cursor begin, Cursor end)
			{
				Begin = begin;
				End = end;
			}

			public Cursor Begin;
			public Cursor End;
		}

		public static void Write(ref Message message, Action<Cursor> action)
		{

			lock (Sync)
			{
				var start = Cursor.Current;
				try
				{
					action(start);

					// The following is to 'flush out' any line issues if the console wasn't cleared previously...
					Console.WriteLine();
					Console.SetCursorPosition(0, Console.CursorTop - 1);
				}
				catch (Exception ex)
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine(ex.ToString());
					Console.ResetColor();
					throw ex;
				}
				message = new Message(start, Cursor.Current);
			}
		}

		public static void OverwriteIfSame(ref Message message, Action<Cursor> action)
		{

			lock (Sync)
			{
				if (message == null)
				{
					Write(ref message, action);
					return;
				}

				var start = Cursor.Current;
				try
				{
					if (message != null && start.Equals(message.End))
					{
						message.Begin.MoveTo();
						action(start = Cursor.Current);
						while (Console.CursorTop < message.End.Top)
						{
							Console.WriteLine();
						}
						while (Console.CursorLeft < message.End.Left)
						{
							Console.Write(' ');
						}

						// The following is to 'flush out' any line issues if the console wasn't cleared previously...
						Console.WriteLine();
						Console.SetCursorPosition(0, Console.CursorTop - 1);
					}
					else
					{
						action(start);
					}
				}
				catch (Exception ex)
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine(ex.ToString());
					Console.ResetColor();
					throw ex;
				}
				message = new Message(start, Cursor.Current);

			}
		}

		public static void OverwriteIfSame(ref Message message, Func<bool> condition, Action<Cursor> action)
		{
			lock (Sync)
			{
				if (condition())
					OverwriteIfSame(ref message, action);
				else
					Write(ref message, action);
			}
		}

	}
}
