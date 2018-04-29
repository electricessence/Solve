using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Eater
{
	public enum Step
	{
		Forward,
		TurnLeft,
		TurnRight
	}

	public enum Orientation
	{
		Up,
		Right,
		Down,
		Left
	}

	public static class Steps
	{
		public static Orientation TurnLeft(this Orientation orientation)
		{
			switch (orientation)
			{
				case Orientation.Up:
					return Orientation.Left;
				case Orientation.Right:
					return Orientation.Up;
				case Orientation.Down:
					return Orientation.Right;
				case Orientation.Left:
					return Orientation.Down;
			}

			throw new ArgumentException("Invalid value.", nameof(orientation));
		}

		public static Orientation TurnRight(this Orientation orientation)
		{
			switch (orientation)
			{
				case Orientation.Up:
					return Orientation.Right;
				case Orientation.Right:
					return Orientation.Down;
				case Orientation.Down:
					return Orientation.Left;
				case Orientation.Left:
					return Orientation.Up;
			}

			throw new ArgumentException("Invalid value.", nameof(orientation));
		}

		public static Orientation Turn(this Orientation orientation, Step step)
		{
			switch (step)
			{
				case Step.TurnLeft:
					return orientation.TurnLeft();
				case Step.TurnRight:
					return orientation.TurnRight();
			}

			return orientation;
		}

		public static Point Forward(this Point boundary, Point current, Orientation orientation)
		{
			switch (orientation)
			{
				case Orientation.Up:
					var v = current.Y + 1;
					return new Point(current.X, v);
				case Orientation.Right:
					var r = current.X + 1;
					return new Point(r, current.Y);
				case Orientation.Down:
					return new Point(current.X, current.Y - 1);
				case Orientation.Left:
					return new Point(current.X - 1, current.Y);
			}
			return current;
		}

		public static Point Forward(this GridLocation boundary, Point current, Orientation orientation)
		{
			switch (orientation)
			{
				case Orientation.Up:
					var v = current.Y + 1;
					return v == boundary.Y ? current : new Point(current.X, v);
				case Orientation.Right:
					var r = current.X + 1;
					return r == boundary.X ? current : new Point(r, current.Y);
				case Orientation.Down:
					return current.Y == 0 ? current : new Point(current.X, current.Y - 1);
				case Orientation.Left:
					return current.X == 0 ? current : new Point(current.X - 1, current.Y);
			}
			return current;
		}

		public const char FORWARD = '^';
		public const char TURN_LEFT = '<';
		public const char TURN_RIGHT = '>';

		public static readonly IReadOnlyList<Step> ALL = (new List<Step> { Step.Forward, Step.TurnLeft, Step.TurnRight }).AsReadOnly();

		public static char ToChar(this Step step)
		{
			switch (step)
			{
				case Step.Forward:
					return FORWARD;
				case Step.TurnLeft:
					return TURN_LEFT;
				case Step.TurnRight:
					return TURN_RIGHT;
			}
			throw new ArgumentException("Invalid value.", nameof(step));
		}

		public static Step FromChar(char step)
		{
			switch (step)
			{
				case FORWARD:
					return Step.Forward;
				case TURN_LEFT:
					return Step.TurnLeft;
				case TURN_RIGHT:
					return Step.TurnRight;
			}
			throw new ArgumentException("Invalid value.", nameof(step));
		}

		public static string ToGenomeHash(this IEnumerable<Step> steps)
		{
			var sb = new StringBuilder();
			foreach (var step in steps)
			{
				sb.Append(step.ToChar());
			}
			return sb.ToString();
		}

		public static IEnumerable<Step> FromGenomeHash(string hash)
		{
			foreach (char c in hash)
			{
				yield return FromChar(c);
			}
		}

		public static bool HasConcecutiveTurns(this IEnumerable<Step> steps)
		{
			Step? last = null;
			foreach (var step in steps)
			{
				if (step != Step.Forward && last.HasValue && last.Value != Step.Forward)
					return true;

				last = step;
			}
			return false;
		}

		public static bool Try(this IEnumerable<Step> steps,
			GridLocation boundary, Point start, Point food)
		{
			int energy;
			return Try(steps, boundary, start, food, out energy);
		}

		public static bool Try(this string steps,
			GridLocation boundary, Point start, Point food, out int energy)
		{
			return Try(FromGenomeHash(steps), boundary, start, food, out energy);
		}

		public static bool Try(this IEnumerable<Step> steps,
			GridLocation boundary, Point start, Point food, out int energy)
		{
			if (start.X > boundary.X || start.Y > boundary.Y)
				throw new ArgumentOutOfRangeException(nameof(start), start, "Start exceeds grid boundary.");

			if (food.X > boundary.X || food.Y > boundary.Y)
				throw new ArgumentOutOfRangeException(nameof(food), food, "Food exceeds grid boundary.");

			var current = start;
			var orientation = Orientation.Up;
			energy = 0;

			foreach (var step in steps)
			{
				energy++;

				switch (step)
				{
					case Step.Forward:
						current = boundary.Forward(current, orientation);
						if (current.Equals(food))
							return true;
						break;

					case Step.TurnLeft:
						orientation = orientation.TurnLeft();
						break;

					case Step.TurnRight:
						orientation = orientation.TurnRight();
						break;
				}
			}

			return false;

		}

		public static IEnumerable<Step> Reduce(this IEnumerable<Step> steps)
		{
			var red = ReduceLoop(steps.ToGenomeHash());
			return red == null ? null : Steps.FromGenomeHash(red);
		}


		static readonly string TURN_LEFT_4 = new String(Steps.TURN_LEFT, 4);
		static readonly string TURN_RIGHT_4 = new String(Steps.TURN_RIGHT, 4);

		static readonly string TURN_LEFT_3 = new String(Steps.TURN_LEFT, 3);
		static readonly string TURN_RIGHT_3 = new String(Steps.TURN_RIGHT, 3);

		static readonly string TURN_LEFT_RIGHT = String.Empty + Steps.TURN_LEFT + Steps.TURN_RIGHT;
		static readonly string TURN_RIGHT_LEFT = String.Empty + Steps.TURN_RIGHT + Steps.TURN_LEFT;

		static readonly Regex ENDING_TURNS_REGEX = new Regex("[" + Steps.TURN_LEFT + Steps.TURN_RIGHT + "]+$");

		static string ReduceLoop(string hash)
		{
			string outerReduced;
			var reduced = hash;

			do
			{
				outerReduced = reduced;
				reduced = ENDING_TURNS_REGEX.Replace(reduced, String.Empty); // Turns at the end are superfluous.
				var reducedLoop = reduced;

				do
				{
					reduced = reducedLoop;
					reducedLoop = reducedLoop.Replace(TURN_LEFT_4, String.Empty);
					reducedLoop = reducedLoop.Replace(TURN_RIGHT_4, String.Empty);

					reducedLoop = reducedLoop.Replace(TURN_LEFT_RIGHT, String.Empty);
					reducedLoop = reducedLoop.Replace(TURN_RIGHT_LEFT, String.Empty);

				}
				while (reduced != reducedLoop);

				do
				{
					reduced = reducedLoop;
					reducedLoop = reducedLoop.Replace(TURN_LEFT_3, String.Empty + Steps.TURN_RIGHT);
					reducedLoop = reducedLoop.Replace(TURN_RIGHT_3, String.Empty + Steps.TURN_LEFT);

					reducedLoop = reducedLoop.Replace(TURN_LEFT_RIGHT, String.Empty);
					reducedLoop = reducedLoop.Replace(TURN_RIGHT_LEFT, String.Empty);

				}
				while (reduced != reducedLoop);

			}
			while (reduced != outerReduced);


			return reduced != hash ? reduced : null;
		}

		public static IEnumerable<Point> Draw(this IEnumerable<Step> steps)
		{
			var current = new Point(0, 0);
			var orientation = Orientation.Up;

			yield return current;

			foreach (var step in steps)
			{
				switch (step)
				{
					case Step.Forward:
						current = current.Forward(current, orientation);
						yield return current;
						break;

					case Step.TurnLeft:
						orientation = orientation.TurnLeft();
						break;

					case Step.TurnRight:
						orientation = orientation.TurnRight();
						break;
				}
			}
		}

		public static void Fill(this Bitmap target, Color color)
		{
			for (var y = 0; y < target.Height; y++)
			{
				for (var x = 0; x < target.Width; x++)
				{
					target.SetPixel(x, y, color);
				}
			}
		}

		public static Bitmap Render(this IEnumerable<Step> steps, int bitScale = 3)
		{
			if (bitScale < 1) throw new ArgumentOutOfRangeException(nameof(bitScale), bitScale, "Must be at least 1.");
			var points = steps.Draw().ToArray();
			var length = points.Length;
			double maxPenBrightness = 160d;
			double colorStep = maxPenBrightness / length;

			var pointsX = points.Select(p => p.X).ToArray();
			var pointsY = points.Select(p => p.Y).ToArray();
			var min = new Point(pointsX.Min(), pointsY.Min());
			var max = new Point(pointsX.Max(), pointsY.Max());
			var boundary = new Point(max.X - min.X, max.Y - min.Y);

			// Help center it...
			var square = Math.Max(boundary.X, boundary.Y);
			var dXY = boundary.X - boundary.Y;
			var dX = dXY < 0 ? (square - boundary.X) / 2 : 0;
			var dY = dXY > 0 ? (square - boundary.Y) / 2 : 0;
			var offset = new Point(-min.X + dX, -min.Y + dY);

			var squareSize = square * bitScale + 2 * bitScale;
			var bitmap = new Bitmap(squareSize, squareSize);
			bitmap.Fill(Color.White);
			Point? first = null;
			Point? last = null;
			for (var i = 1; i < length; i++)
			{
				var p = points[i];
				var o = new Point((p.X + offset.X) * bitScale + bitScale, (p.Y + offset.Y) * bitScale + bitScale);
				var brightness = Convert.ToInt32(maxPenBrightness - i * colorStep);
				var color = Color.FromArgb(brightness, brightness, brightness);
				if (!last.HasValue)
				{
					first = o;
					last = o;
				}
				p = last.Value;
				for (var y = Math.Min(p.Y, o.Y); y <= Math.Max(p.Y, o.Y); y++)
				{
					for (var x = Math.Min(p.X, o.X); x <= Math.Max(p.X, o.X); x++)
					{
						Debug.Assert(x >= 0);
						Debug.Assert(y >= 0);
						Debug.Assert(x < bitmap.Width);
						Debug.Assert(y < bitmap.Height);
						try
						{
							bitmap.SetPixel(x, y, color);
						}
						catch (Exception ex)
						{
							Console.WriteLine($"({x},{y})/{squareSize}: {ex.Message}");
							Debugger.Break();
						}
					}
				}
				last = o;
			}

			bitmap.SetPixel(first.Value.X, first.Value.Y, Color.Green);
			bitmap.SetPixel(last.Value.X, last.Value.Y, Color.Red);

			return bitmap;
		}
	}

}
