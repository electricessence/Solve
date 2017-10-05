
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Open.Arithmetic;
using Open.Collections;
using Open.Text;
using Open.Numeric;
using Nito.AsyncEx;
using Open.Numeric.Precision;

namespace Solve
{

	public class SingleFitness : IComparable<SingleFitness>
	{
		public readonly double MaxScore;
		ProcedureResult _result;
		object _sync = new Object();
		public SingleFitness(IEnumerable<double> scores = null, double maxScore = 1) : this(new ProcedureResult(0, 0), maxScore)
		{
			if (scores != null)
				Add(scores);
		}

		public SingleFitness(ProcedureResult initial, double maxScore = 1) : base()
		{
			_result = initial;
			MaxScore = maxScore;
		}

		public SingleFitness(double maxScore) : this(null, maxScore)
		{
		}


		public ProcedureResult Result
		{
			get { return _result; }
		}


		public void Add(double value, int count = 1)
		{
			if (count < 0)
				throw new ArgumentOutOfRangeException("count", count, "Count cannot be negative.");

			Debug.Assert(count > 0, "Must add a value greater than zero.");
			if (count != 0)
			{

				Debug.Assert(!double.IsNaN(value), "Adding a NaN value will completely invalidate the fitness value.");
				Debug.Assert(value <= MaxScore, "Adding a score that is above the maximum will potentially invalidate the current run.");
				// Ensures 1 update at a time.
				lock (_sync) _result = _result.Add(value, count);
			}
		}

		public void Add(ProcedureResult other)
		{
			Debug.Assert(!double.IsNaN(other.Average), "Adding a NaN value will completely invalidate the fitness value.");
			Debug.Assert(other.Average <= MaxScore, "Adding a score that is above the maximum will potentially invalidate the current run.");
			// Ensures 1 update at a time.
			lock (_sync) _result += other;
		}

		public void Add(IEnumerable<double> values)
		{
			double sum = 0;
			int count = 0;
			foreach (var value in values)
			{
				sum += value;
				count++;
			}
			Add(sum, count);
		}

		// Allow for custom comparison of individual fitness types.
		// By default, it' simply the average regardless of number of samples.
		public virtual int CompareTo(SingleFitness other)
		{
			if (this == other) return 0;
			if (other == null)
				throw new ArgumentNullException("other");

			// Check for weird averages that push the values above maximum and adjust.  (Bounce off the barrier.)   See above for debug assertions.

			var a = _result;
			if (a.Average > MaxScore)
				a = new ProcedureResult(MaxScore * a.Count, a.Count);
			var b = other._result;
			if (b.Average > MaxScore)
				b = new ProcedureResult(MaxScore * b.Count, b.Count);

			return a.CompareTo(b);
		}

	}

	public interface IFitness : IComparable<IFitness>
	{
		int SampleCount { get; }
		IReadOnlyList<double> Scores { get; }

		int Count { get; }

		long ID { get; }

		ProcedureResult GetResult(int index);
		double GetScore(int index);
	}

	public struct FitnessScore : IFitness
	{
		readonly ProcedureResult[] _results;

		public FitnessScore(IFitness source)
		{
			var len = source.Count;
			Count = len;
			ID = source.ID;
			SampleCount = source.SampleCount;
			var results = _results = new ProcedureResult[len];
			for (var i = 0; i < len; i++)
				results[i] = source.GetResult(i);
			_scores = Lazy.Create(() => results.Select(s => s.Average).ToList().AsReadOnly());
		}

		public int Count
		{
			get;
			private set;
		}

		public long ID
		{
			get;
			private set;
		}

		public int SampleCount
		{
			get;
			private set;
		}

		Lazy<ReadOnlyCollection<double>> _scores;
		public IReadOnlyList<double> Scores
		{
			get
			{
				return _scores.Value;
			}
		}

		public int CompareTo(IFitness other)
		{
			return Fitness.Comparison(this, other);
		}

		public ProcedureResult GetResult(int index)
		{
			return _results[index];
		}

		public double GetScore(int index)
		{
			return _results[index].Average;
		}
	}

	public class Fitness : TrackedList<SingleFitness>, IFitness
	{

		public Fitness() //: base(new AsyncReadWriteModificationSynchronizer())
		{
			ID = Interlocked.Increment(ref FitnessCount);
		}

		public int SampleCount
		{
			get
			{
				if (Count == 0) return 0;
				return Sync.Reading(() => this.Min(s => s.Result.Count));
			}
		}

		public ProcedureResult GetResult(int index)
		{
			return this[index].Result;
		}

		public double GetScore(int index)
		{
			return GetResult(index).Average;
		}

		public IReadOnlyList<double> Scores
		{
			get
			{
				return Sync.Reading(() => this.Select(v => v.Result.Average).ToList())
					.AsReadOnly();
			}
		}

		public void Add(ProcedureResult score)
		{
			Sync.Modifying(() =>
			{
				this.Add(new SingleFitness(score));
			});
		}

		public void AddTheseScores(IEnumerable<double> scores)
		{
			Sync.Modifying(() =>
			{
				var i = 0;
				var count = Count;
				foreach (var n in scores)
				{
					SingleFitness f;
					if (i < count)
					{
						f = this[i];
					}
					else
					{
						this.Add(f = new SingleFitness());
					}

					f.Add(n);
					i++;
				}
			});

		}

		public Fitness Merge(IFitness other)
		{

			AssertIsAlive();

			if (other.Count != 0)
			{
				if (_source.Count != 0 && other.Count != Count)
					throw new InvalidOperationException("Cannot add fitness values where the count doesn't match.");

				Sync.Modifying(() =>
				{
					var count = other.Count;
					for (var i = 0; i < count; i++)
					{
						var r = other.GetResult(i);
						if (i < _source.Count) _source[i].Add(r);
						else _source.Add(new SingleFitness(r));
					}
				});
			}

			return this;
		}
		public void AddScores(params double[] scores)
		{
			this.AddTheseScores(scores);
		}

		// Allowing for a rejection count opens the possiblity for a second chance.

		int _rejectionCount = 0;
		public int RejectionCount
		{
			get
			{
				return _rejectionCount;
			}
			set
			{
				Interlocked.Exchange(ref _rejectionCount, value);
			}
		}

		public int IncrementRejection()
		{
			return Interlocked.Increment(ref _rejectionCount);
		}

		static long FitnessCount = 0;
		public long ID
		{
			get;
			private set;
		}

		// internal int TestingCount = 0;

		// Some cases enumerables are easier to sort in ascending than descending so "Top" in this respect means 'First'.
		public const int ORDER_DIRECTION = -1;
		public int CompareTo(IFitness other)
		{
			return Comparison(this, other);
		}

		public class Comparer : IComparer<IFitness>
		{
			public int Compare(IFitness x, IFitness y)
			{
				return Comparison(x, y);
			}
		}

		public static int Comparison(IFitness x, IFitness y)
		{
			int c;

			if (x == y) return 0;

			c = ValueComparison(x, y);
			if (c != 0) return c;

			c = IdComparison(x, y);
			if (c != 0) return c;

			throw new Exception("Impossible? Interlocked failed?");

		}

		public static int ValueComparison(IFitness x, IFitness y)
		{
			if (x == y) return 0;
			if (y == null)
				throw new ArgumentNullException("other");
			int xLen = x.Count, yLen = y.Count;
			if (xLen != 0 || yLen != 0)
			{
				// Untested needs at least one test before being ordered.
				// It's also possible to fail adding because NaN so avoid.
				if (xLen == 0 && yLen != 0) return -ORDER_DIRECTION;
				if (xLen != 0 && yLen == 0) return +ORDER_DIRECTION;
				Debug.Assert(xLen == yLen, "Fitnesses must be compatible.");

				// In non-debug, all for the lesser scored to be of lesser importance.
				if (xLen < yLen) return -ORDER_DIRECTION;
				if (xLen > yLen) return +ORDER_DIRECTION;

				for (var i = 0; i < xLen; i++)
				{
					var sx = x.GetResult(i);
					var sy = y.GetResult(i);

					var c = sx.CompareTo(sy);
					if (c != 0) return c * ORDER_DIRECTION;
				}
			}

			return 0;
		}

		public static int IdComparison(IFitness x, IFitness y)
		{
			if (x == y) return 0;
			if (x.ID < y.ID) return +ORDER_DIRECTION;
			if (x.ID > y.ID) return -ORDER_DIRECTION;
			return 0;
		}

		AsyncLock _lock;
		public AsyncLock Lock
		{
			get
			{
				return LazyInitializer.EnsureInitialized(ref _lock);
			}
		}

	}

	public static class FitnessExtensions
	{
		public static bool HasConverged(this IFitness fitness, uint minSamples = 100, double convergence = 1, double tolerance = 0)
		{
			if (minSamples > fitness.SampleCount) return false;
			foreach (var s in fitness.Scores)
			{
				if (s > convergence + double.Epsilon)
					throw new Exception("Score has exceeded convergence value: " + s);
				if (s.IsNearEqual(convergence, 0.0000001) && s.ToString() == convergence.ToString())
					continue;
				if (s < convergence - tolerance)
					return false;
			}
			return true;
		}

		public static FitnessScore SnapShot(this IFitness fitness)
		{
			return new FitnessScore(fitness);
		}

		public static Fitness Merge(this IEnumerable<IFitness> fitnesses)
		{
			return fitnesses.Aggregate(new Fitness(), (prev, current) => prev.Merge(current));
		}
	}
}