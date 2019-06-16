using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Eater
{
	public enum Step : byte
	{
		Forward,
		TurnRight,
		TurnLeft
	}

	public struct StepCount
	{
		public Step Step;
		public int Count;

		public StepCount(Step step = Step.Forward, int count = 1)
		{
			Step = step;
			Count = count;
		}

		public static StepCount operator +(StepCount a, StepCount b)
		{
			if (a.Step != b.Step)
				throw new InvalidOperationException("Attempting to combine two StepCounts with different steps.");
			return new StepCount(a.Step, a.Count + b.Count);
		}

		public static StepCount operator ++(StepCount a)
		{
			return new StepCount(a.Step, a.Count + 1);
		}

		public static StepCount operator --(StepCount a)
		{
			return new StepCount(a.Step, a.Count - 1);
		}

		public override string ToString()
		{
			if (Count < 1) throw new InvalidOperationException($"Step count is less than 1. ({Count})");
			return (Count == 1 ? string.Empty : Count.ToString()) + Step.ToChar();
		}
	}

	public enum Orientation
	{
		Up,
		Right,
		Down,
		Left
	}

	public static class StepExtensions
	{
		public static IEnumerable<Step> Steps(this IEnumerable<StepCount> steps)
			=> steps.SelectMany(s => Enumerable.Repeat(s.Step, s.Count));

		public static IEnumerable<StepCount> ToStepCounts(this IEnumerable<Step> steps)
		{
			var s = steps as Step[] ?? steps.ToArray();
			var len = s.Length;
			if (len == 0) yield break;

			var last = new StepCount()
			{
				Step = s[0],
				Count = 1
			};

			for (var i = 1; i < len; i++)
			{
				var step = s[i];
				if (step == last.Step)
				{
					last.Count++;
				}
				else
				{
					yield return last;

					last = new StepCount(step, 1);
				}
			}
			yield return last;

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

		public static string ToGenomeHash(this IEnumerable<StepCount> steps)
		{
			var sb = new StringBuilder();
			foreach (var s in steps)
			{
				if (s.Count != 1)
					sb.Append(s.Count);
				sb.Append(s.Step.ToChar());
			}
			return sb.ToString();
		}

		public static string ToGenomeHash(this ReadOnlySpan<StepCount> steps)
		{
			var sb = new StringBuilder();
			foreach (var s in steps)
			{
				if (s.Count != 1)
					sb.Append(s.Count);
				sb.Append(s.Step.ToChar());
			}
			return sb.ToString();
		}

		public static string ToGenomeHash(this Span<StepCount> steps)
			=> ToGenomeHash((ReadOnlySpan<StepCount>)steps);
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

		public static Point Forward(this Point current, Orientation orientation)
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

		public static Point Forward(this Size boundary, Point current, Orientation orientation)
		{
			switch (orientation)
			{
				case Orientation.Up:
					var v = current.Y + 1;
					return v == boundary.Height ? current : new Point(current.X, v);
				case Orientation.Right:
					var r = current.X + 1;
					return r == boundary.Width ? current : new Point(r, current.Y);
				case Orientation.Down:
					return current.Y == 0 ? current : new Point(current.X, current.Y - 1);
				case Orientation.Left:
					return current.X == 0 ? current : new Point(current.X - 1, current.Y);
			}
			return current;
		}

		public const char FORWARD = '^';
		public const char TURN_RIGHT = '>';
		public const char TURN_LEFT = '<';

		public static readonly IReadOnlyList<Step> ALL = (new List<Step> { Step.Forward, Step.TurnRight, Step.TurnLeft }).AsReadOnly();

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

		static readonly Regex StepReplace = new Regex(@"(\d+)([<>^])", RegexOptions.Compiled);

		public static IEnumerable<Step> FromGenomeHash(string hash)
		{
			hash = StepReplace.Replace(hash, m =>
			{
				var sb = new StringBuilder();
				sb.Append(m.Groups[2].Value[0], int.Parse(m.Groups[1].Value));
				return sb.ToString();
			});

			foreach (var c in hash)
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
			Size boundary, Point start, Point food)
			=> Try(steps, boundary, start, food, out _);

		public static bool Try(this string steps,
			Size boundary, Point start, Point food, out int energy)
			=> Try(FromGenomeHash(steps), boundary, start, food, out energy);

		public static bool Try(this IEnumerable<Step> steps,
			Size boundary, Point start, Point food, out int energy)
		{
			if (start.X > boundary.Width || start.Y > boundary.Height)
				throw new ArgumentOutOfRangeException(nameof(start), start, "Start exceeds grid boundary.");

			if (food.X > boundary.Width || food.Y > boundary.Height)
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
			return red == null ? null : FromGenomeHash(red);
		}

		static readonly string TURN_LEFT_4 = new string(TURN_LEFT, 4);
		static readonly string TURN_RIGHT_4 = new string(TURN_RIGHT, 4);

		static readonly string TURN_LEFT_3 = new string(TURN_LEFT, 3);
		static readonly string TURN_RIGHT_3 = new string(TURN_RIGHT, 3);

		static readonly string TURN_LEFT_RIGHT = string.Empty + TURN_LEFT + TURN_RIGHT;
		static readonly string TURN_RIGHT_LEFT = string.Empty + TURN_RIGHT + TURN_LEFT;

		static readonly Regex ENDING_TURNS_REGEX = new Regex("[" + TURN_LEFT + TURN_RIGHT + "]+$");

		static string ReduceLoop(string hash)
		{
			string outerReduced;
			var reduced = hash;

			do
			{
				outerReduced = reduced;
				reduced = ENDING_TURNS_REGEX.Replace(reduced, string.Empty); // Turns at the end are superfluous.
				var reducedLoop = reduced;

				do
				{
					reduced = reducedLoop;
					reducedLoop = reducedLoop.Replace(TURN_LEFT_4, string.Empty);
					reducedLoop = reducedLoop.Replace(TURN_RIGHT_4, string.Empty);

					reducedLoop = reducedLoop.Replace(TURN_LEFT_RIGHT, string.Empty);
					reducedLoop = reducedLoop.Replace(TURN_RIGHT_LEFT, string.Empty);

				}
				while (reduced != reducedLoop);

				do
				{
					reduced = reducedLoop;
					reducedLoop = reducedLoop.Replace(TURN_LEFT_3, string.Empty + TURN_RIGHT);
					reducedLoop = reducedLoop.Replace(TURN_RIGHT_3, string.Empty + TURN_LEFT);

					reducedLoop = reducedLoop.Replace(TURN_LEFT_RIGHT, string.Empty);
					reducedLoop = reducedLoop.Replace(TURN_RIGHT_LEFT, string.Empty);

				}
				while (reduced != reducedLoop);

			}
			while (reduced != outerReduced);


			return reduced != hash ? reduced : null;
		}

		public static IEnumerable<Point> Draw(this IEnumerable<Step> steps, bool pointToPoint = false)
		{
			var current = new Point(0, 0);
			var orientation = Orientation.Up;
			var moved = false;

			foreach (var step in steps)
			{
				switch (step)
				{
					case Step.Forward:
						if (!pointToPoint || !moved)
							yield return current;

						current = current.Forward(orientation);
						moved = true;
						break;

					case Step.TurnLeft:
						moved = false;
						orientation = orientation.TurnLeft();
						break;

					case Step.TurnRight:
						moved = false;
						orientation = orientation.TurnRight();
						break;
				}
			}

			if (moved)
				yield return current;
		}

		public static IEnumerable<Point> InvertY(this IEnumerable<Point> points)
			=> points.Select(p => new Point(p.X, -p.Y));

		public static void Fill(this Bitmap target, Color color)
		{
			using (var graphics = Graphics.FromImage(target))
				graphics.Clear(color);
		}

		public static Bitmap Render(this IEnumerable<Step> steps, int bitScale = 3)
		{
			if (bitScale < 1) throw new ArgumentOutOfRangeException(nameof(bitScale), bitScale, "Must be at least 1.");
			var points = steps.Draw().InvertY().ToArray();
			var length = points.Length;
			const double maxPenBrightness = 160d;
			var colorStep = maxPenBrightness / length;

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
			for (var i = 0; i < length; i++)
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

			if (first.HasValue)
				bitmap.SetPixel(first.Value.X, first.Value.Y, Color.Green);
			if (last.HasValue)
				bitmap.SetPixel(last.Value.X, last.Value.Y, Color.Red);

			return bitmap;
		}

		public static Bitmap Render2(this IEnumerable<Step> steps, ushort scaleMultiple = 1)
		{
			var scale = 4 * scaleMultiple;
			var bitScale = 16 * scale;
			if (bitScale < 1) throw new ArgumentOutOfRangeException(nameof(bitScale), bitScale, "Must be at least 1.");
			var points = steps.Draw(true).InvertY().ToArray();
			var length = points.Length;
			const double maxPenBrightness = 160d;
			var colorStep = maxPenBrightness / length;

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

			var squareSize = square * bitScale + 2 * bitScale + points.Length;
			Bitmap bitmap;
			for (var i = 0; ; i++)
			{
				try
				{
					bitmap = new Bitmap(squareSize, squareSize);
					bitmap.Fill(Color.White);
					break;
				}
				catch (Exception)
				{
					if (i > 9) throw;
				}
			}

			if (points.Length == 0) return bitmap;

			using (var graphic = Graphics.FromImage(bitmap))
			{
				var pointFs = points.Select((p, i) => new PointF((p.X + offset.X) * bitScale + bitScale + i, (p.Y + offset.Y) * bitScale + bitScale + i)).ToArray();
				var first = pointFs.First();
				var last = pointFs.Last();
				var radius = 5 * scale;

				graphic.DrawRectangle(new Pen(Color.Green, 3 * scale), first.X - radius, first.Y - radius, radius * 2, radius * 2);
				graphic.DrawRectangle(new Pen(Color.Red, 3 * scale), last.X - radius, last.Y - radius, radius * 2, radius * 2);


				var outlinePen = new Pen(Color.FromArgb(100, Color.White), 8 * scale);
				outlinePen.SetLineCap(LineCap.Round, LineCap.Round, DashCap.Round);
				for (var i = 0; i < length; i++)
				{
					if (i < length - 1)
						graphic.DrawLine(outlinePen, pointFs[i], pointFs[i + 1]);

					if (i <= 0) continue;
					var brightness = Convert.ToInt32(maxPenBrightness - i * colorStep);
					var color = Color.FromArgb(brightness, brightness, brightness);
					var pen = new Pen(color, 4 * scale);
					pen.SetLineCap(LineCap.Round, LineCap.Round, DashCap.Round);
					graphic.DrawLine(pen, pointFs[i - 1], pointFs[i]);

				}


				graphic.DrawRectangle(new Pen(Color.FromArgb(128, Color.Green), 3 * scale), first.X - radius, first.Y - radius, radius * 2, radius * 2);
				graphic.DrawRectangle(new Pen(Color.FromArgb(128, Color.Red), 3 * scale), last.X - radius, last.Y - radius, radius * 2, radius * 2);

			}


			return bitmap;
		}

	}

}
