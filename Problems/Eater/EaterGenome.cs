using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Solve;
using System.Text;
using System.Linq;
using System.Collections;

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
			switch(orientation)
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

			throw new ArgumentException("Invalid orientation value.");
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

			throw new ArgumentException("Invalid orientation value.");
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

		public static GridLocation Forward(this GridLocation boundary, GridLocation current, Orientation orientation)
		{
			switch (orientation)
			{
				case Orientation.Up:
					var v = current.Y + 1;
					return v == boundary.Y ? current : new GridLocation(current.X, v);
				case Orientation.Right:
					var r = current.X + 1;
					return r == boundary.X ? current : new GridLocation(r, current.Y);
				case Orientation.Down:
					return current.Y == 0 ? current : new GridLocation(current.X, current.Y - 1);
				case Orientation.Left:
					return current.X == 0 ? current : new GridLocation(current.X - 1, current.Y);
			}
			return current;
		}

		public const char FORWARD = '↑';
		public const char TURN_LEFT = '↰';
		public const char TURN_RIGHT = '↱';

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
			throw new ArgumentException("Invalid step value.");
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
			throw new ArgumentException("Invalid step value.");
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
			GridLocation boundary, GridLocation start, GridLocation food, out int energy)
		{
			if (start.X > boundary.X || start.Y > boundary.Y)
				throw new ArgumentOutOfRangeException("start", start, "Start exceeds grid boundary.");

			if (food.X > boundary.X || food.Y > boundary.Y)
				throw new ArgumentOutOfRangeException("food", food, "Food exceeds grid boundary.");

			var current = start;
			var orientation = Orientation.Up;
			energy = 0;

			foreach (var step in steps)
			{
				energy++;

				switch(step)
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
	}


	public sealed class EaterGenome : GenomeBase<Step>, ICloneable<EaterGenome>, IReducible<EaterGenome>, IEnumerable<Step>
	{

		static readonly Step[] EMPTY = new Step[0];

		public EaterGenome() : base()
		{
			_steps = EMPTY;
		}

		public EaterGenome(IEnumerable<Step> steps) : base()
		{
			Freeze(steps);
		}


		Step[] _steps;

		string _hash;
		public override string Hash
		{
			get
			{
				return _hash ?? _steps.ToGenomeHash();
			}
		}

		public bool IsReducible
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public new EaterGenome Clone()
		{
			return new EaterGenome(this._steps);
		}

		protected override object CloneInternal()
		{
			return this.Clone();
		}

		protected override Step[] GetGenes()
		{
			return _steps.ToArray();
		}

		protected override void OnBeforeFreeze()
		{
			_hash = _steps.ToGenomeHash();
		}

		public void Freeze(IEnumerable<Step> steps)
		{
			_steps = steps.ToArray();
			if (Debugger.IsAttached)
			{
				// Validate that reduction isn't being trampled in weird ways.
				var reduced = Reduce() ?? this;
				LazyInitializer.EnsureInitialized(ref _reduced, () => reduced);
				Debug.Assert(_reduced.Hash == reduced.Hash, "Existing reduction does not match actual reduction..");
			}
			else
			{
				LazyInitializer.EnsureInitialized(ref _reduced, ()=> Reduce() ?? this);
			}

			this.Freeze();
		}

		EaterGenome Reduce()
		{
			var reducedSteps = ReduceLoop(_steps);
			return reducedSteps == null ? null : new EaterGenome(reducedSteps);
		}

		static readonly string TURN_LEFT_4 = new String(Steps.TURN_LEFT, 4);
		static readonly string TURN_RIGHT_4 = new String(Steps.TURN_RIGHT, 4);

		static readonly string TURN_LEFT_3 = new String(Steps.TURN_LEFT, 3);
		static readonly string TURN_RIGHT_3 = new String(Steps.TURN_RIGHT, 3);

		static readonly string TURN_LEFT_RIGHT = String.Empty + Steps.TURN_LEFT + Steps.TURN_RIGHT;
		static readonly string TURN_RIGHT_LEFT = String.Empty + Steps.TURN_RIGHT + Steps.TURN_LEFT;

		public static IEnumerable<Step> ReduceLoop(IEnumerable<Step> steps)
		{
			var red = ReduceLoop(steps.ToGenomeHash());
			return red == null ? null : Steps.FromGenomeHash(red);
		}

		public static string ReduceLoop(string hash)
		{
			string outerReduced;
			var reduced = hash;

			do
			{
				outerReduced = reduced;
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

		EaterGenome _reduced;
		public EaterGenome AsReduced(bool ensureClone = false)
		{
			EaterGenome reduced;
			if (!IsReadOnly)
			{
				reduced = Reduce();
				if (reduced == null) reduced = ensureClone ? this.Clone() : this;
			}
			else
			{
				reduced = ensureClone ? _reduced.Clone() : _reduced;
			}
			return reduced;
		}

		public void ReplaceReduced(EaterGenome reduced)
		{
			if (reduced == null)
				throw new ArgumentNullException("reduced");
			if (_reduced != reduced)
			{
				if (IsReadOnly && _reduced != null && _reduced.Hash != reduced.Hash)
					throw new InvalidOperationException("Attempting to replace a reduced genome with one that doesn't match.");
				_reduced = reduced;
			}
		}

		public IEnumerator<Step> GetEnumerator()
		{
			return ((IEnumerable<Step>)_steps).GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}
	}
}
