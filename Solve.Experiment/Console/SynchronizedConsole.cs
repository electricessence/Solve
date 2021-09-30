using System;

namespace Solve.Experiment.Console
{
	public static class SynchronizedConsole
	{
		public static object Sync => Cursor.Sync;

		public static void Write(ref CursorRange message, in Action<Cursor> action)
		{

			lock (Sync)
			{
				var start = Cursor.Current;
				try
				{
					action(start);

					// The following is to 'flush out' any line issues if the console wasn't cleared previously...
					System.Console.WriteLine();
					System.Console.SetCursorPosition(0, System.Console.CursorTop - 1);
				}
				catch (Exception ex)
				{
					System.Console.ForegroundColor = ConsoleColor.Red;
					System.Console.WriteLine(ex.ToString());
					System.Console.ResetColor();
					throw;
				}
				message = new CursorRange(start, Cursor.Current);
			}
		}

		public static void OverwriteIfSame(ref CursorRange message, in Action<Cursor> action)
		{

			lock (Sync)
			{
				if (message is null)
				{
					Write(ref message, action);
					return;
				}

				var start = Cursor.Current;
				try
				{
					if (message is not null && start.Equals(message.End))
					{
						message.Begin.MoveTo();
						action(start = Cursor.Current);
						while (System.Console.CursorTop < message.End.Top)
						{
							System.Console.WriteLine();
						}
						while (System.Console.CursorLeft < message.End.Left)
						{
							System.Console.Write(' ');
						}

						// The following is to 'flush out' any line issues if the console wasn't cleared previously...
						System.Console.WriteLine();
						System.Console.SetCursorPosition(0, System.Console.CursorTop - 1);
					}
					else
					{
						action(start);
					}
				}
				catch (Exception ex)
				{
					System.Console.ForegroundColor = ConsoleColor.Red;
					System.Console.WriteLine(ex.ToString());
					System.Console.ResetColor();
					throw;
				}
				message = new CursorRange(in start, Cursor.Current);

			}
		}

		public static void OverwriteIfSame(ref CursorRange message, Func<bool> condition, in Action<Cursor> action)
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
